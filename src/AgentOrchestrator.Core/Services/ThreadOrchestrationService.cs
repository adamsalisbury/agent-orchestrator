using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Services;

public class ThreadOrchestrationService
{
    private const int MaxDelegationDepth = 5;

    private readonly IThreadRepository _threadRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IClaudeCodeRunner _runner;
    private readonly Channel<QueueItem> _queue;
    private readonly ConcurrentDictionary<string, ConsultationCallback> _callbacks = new();

    public ThreadOrchestrationService(
        IThreadRepository threadRepository,
        IAgentRepository agentRepository,
        IClaudeCodeRunner runner,
        IProjectRepository projectRepository)
    {
        _threadRepository = threadRepository;
        _agentRepository = agentRepository;
        _runner = runner;
        _projectRepository = projectRepository;
        _queue = Channel.CreateUnbounded<QueueItem>();
        Task.Run(ProcessQueueAsync);
    }

    public async Task<ThreadMessage> SendMessageAsync(string agentId, string? threadId, string content)
    {
        threadId ??= GenerateId();

        var existingMessages = await _threadRepository.GetThreadMessagesAsync(agentId, threadId);
        var nextNumber = existingMessages.Count > 0
            ? existingMessages.Max(m => m.MessageNumber) + 1
            : 1;

        var userMessage = new ThreadMessage
        {
            ThreadId = threadId,
            MessageNumber = nextNumber,
            Direction = MessageDirection.Outbound,
            Content = content,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Completed
        };
        await _threadRepository.SaveMessageAsync(agentId, userMessage);

        var agentMessage = new ThreadMessage
        {
            ThreadId = threadId,
            MessageNumber = nextNumber + 1,
            Direction = MessageDirection.Inbound,
            Content = string.Empty,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Pending
        };
        await _threadRepository.SaveMessageAsync(agentId, agentMessage);

        await _queue.Writer.WriteAsync(new QueueItem(agentId, threadId, nextNumber + 1));

        return userMessage;
    }

    public async Task<List<ThreadMessage>> GetAllMessagesAsync(string agentId)
    {
        return await _threadRepository.GetAllMessagesAsync(agentId);
    }

    public async Task<List<ThreadMessage>> GetThreadMessagesAsync(string agentId, string threadId)
    {
        return await _threadRepository.GetThreadMessagesAsync(agentId, threadId);
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync())
        {
            var message = await _threadRepository.GetMessageAsync(item.AgentId, item.ThreadId, item.MessageNumber);
            if (message == null) continue;

            if (message.Status != MessageStatus.Processing)
            {
                message.Status = MessageStatus.Processing;
                await _threadRepository.SaveMessageAsync(item.AgentId, message);
            }

            // Mark agent as busy with current task
            var firstUserMessage = (await _threadRepository.GetThreadMessagesAsync(item.AgentId, item.ThreadId))
                .Where(m => m.Direction == MessageDirection.Outbound && m.Status == MessageStatus.Completed)
                .OrderBy(m => m.MessageNumber)
                .LastOrDefault();
            var taskSummary = firstUserMessage?.Content ?? "Processing request";
            if (taskSummary.Length > 120) taskSummary = taskSummary[..120] + "...";
            await _agentRepository.SetCurrentTaskAsync(item.AgentId, taskSummary);

            try
            {
                var allMessages = await _threadRepository.GetThreadMessagesAsync(item.AgentId, item.ThreadId);
                var prior = allMessages
                    .Where(m => m.MessageNumber < item.MessageNumber && m.Status == MessageStatus.Completed)
                    .OrderBy(m => m.MessageNumber)
                    .ToList();

                var agent = await _agentRepository.GetAsync(item.AgentId);
                var teamAgents = await _agentRepository.GetAllAsync();
                var project = await _projectRepository.GetAsync();

                // Determine working directory: developers use their own workspace
                string workingDir;
                if (agent?.IsDeveloper == true)
                {
                    workingDir = _agentRepository.GetAgentWorkspacePath(item.AgentId);
                    Directory.CreateDirectory(workingDir);
                }
                else
                {
                    workingDir = _projectRepository.GetWorkspacePath();
                }

                // Get connected agents (manager + direct reports) for delegation
                var connectedAgents = GetConnectedAgents(agent, teamAgents);

                bool allowDelegation = !item.IsConsultation && item.DelegationDepth < MaxDelegationDepth && connectedAgents.Count > 0;
                var prompt = BuildPromptWithContext(agent, teamAgents, connectedAgents, prior, allowDelegation, item.ConsultationContext, project);
                var response = await _runner.ExecuteAsync(prompt, workingDir);

                var delegation = allowDelegation ? TryParseDelegation(response) : null;

                if (delegation != null)
                {
                    // Validate delegation target is a connected agent
                    var targetAgent = connectedAgents.FirstOrDefault(a =>
                        a.Name.Equals(delegation.ToAgent, StringComparison.OrdinalIgnoreCase));

                    if (targetAgent != null)
                    {
                        // Mark as blocked, waiting on the target agent
                        await _agentRepository.SetCurrentTaskAsync(
                            item.AgentId,
                            $"Waiting for {targetAgent.Name} ({targetAgent.JobTitle})",
                            targetAgent.Id, targetAgent.Name);

                        var handled = await HandleDelegation(delegation, agent, targetAgent, item);
                        if (handled)
                            continue;
                    }
                }

                message.Content = response;
                message.Status = MessageStatus.Completed;
                message.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Content = $"Error: {ex.Message}";
                message.Status = MessageStatus.Failed;
                message.SentAt = DateTime.UtcNow;
            }

            await _threadRepository.SaveMessageAsync(item.AgentId, message);

            // Clear current task — agent is done
            await _agentRepository.ClearCurrentTaskAsync(item.AgentId);

            var callbackKey = $"{item.AgentId}/{item.ThreadId}/{item.MessageNumber}";
            if (_callbacks.TryRemove(callbackKey, out var callback))
            {
                await HandleConsultationComplete(callback, message, item);
            }
        }
    }

    private static List<Agent> GetConnectedAgents(Agent? agent, List<Agent> allAgents)
    {
        if (agent == null) return new List<Agent>();

        var connected = new List<Agent>();

        // Manager (the agent this one reports to)
        if (!string.IsNullOrEmpty(agent.ReportsToId))
        {
            var manager = allAgents.FirstOrDefault(a => a.Id == agent.ReportsToId);
            if (manager != null)
                connected.Add(manager);
        }

        // Direct reports (agents that report to this one)
        var reports = allAgents.Where(a => a.ReportsToId == agent.Id).ToList();
        connected.AddRange(reports);

        return connected;
    }

    private async Task<bool> HandleDelegation(
        DelegationDirective delegation,
        Agent? sourceAgent,
        Agent targetAgent,
        QueueItem item)
    {
        var newContext = item.ConsultationContext ?? "";
        newContext += $"\n[You asked {targetAgent.Name} ({targetAgent.JobTitle})]: {delegation.Question}\n";

        var consultThreadId = GenerateId();

        var consultQuestion = new ThreadMessage
        {
            ThreadId = consultThreadId,
            MessageNumber = 1,
            Direction = MessageDirection.Outbound,
            Content = $"{sourceAgent?.Name ?? "An agent"} ({sourceAgent?.JobTitle ?? "unknown role"}) is consulting you:\n\n{delegation.Question}",
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Completed,
            FromAgentId = sourceAgent?.Id,
            FromAgentName = sourceAgent?.Name
        };
        await _threadRepository.SaveMessageAsync(targetAgent.Id, consultQuestion);

        var consultResponse = new ThreadMessage
        {
            ThreadId = consultThreadId,
            MessageNumber = 2,
            Direction = MessageDirection.Inbound,
            Content = string.Empty,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Pending
        };
        await _threadRepository.SaveMessageAsync(targetAgent.Id, consultResponse);

        _callbacks[$"{targetAgent.Id}/{consultThreadId}/2"] = new ConsultationCallback(
            item.AgentId,
            item.ThreadId,
            item.MessageNumber,
            newContext,
            item.DelegationDepth + 1);

        await _queue.Writer.WriteAsync(new QueueItem(
            targetAgent.Id, consultThreadId, 2,
            IsConsultation: true));

        return true;
    }

    private async Task HandleConsultationComplete(
        ConsultationCallback callback,
        ThreadMessage consultationResponse,
        QueueItem item)
    {
        var respondingAgent = await _agentRepository.GetAsync(item.AgentId);
        var respondingName = respondingAgent?.Name ?? item.AgentId;

        var updatedContext = callback.AccumulatedContext
            + $"[{respondingName} replied]: {consultationResponse.Content}\n";

        await _queue.Writer.WriteAsync(new QueueItem(
            callback.OriginAgentId,
            callback.OriginThreadId,
            callback.OriginMessageNumber,
            IsConsultation: false,
            ConsultationContext: updatedContext,
            DelegationDepth: callback.Depth));
    }

    private static DelegationDirective? TryParseDelegation(string response)
    {
        var trimmed = response.Trim();

        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            return null;

        try
        {
            var json = trimmed[jsonStart..(jsonEnd + 1)];
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("action", out var action) &&
                action.GetString()?.Equals("delegate", StringComparison.OrdinalIgnoreCase) == true &&
                root.TryGetProperty("toAgent", out var toAgent) &&
                root.TryGetProperty("question", out var question))
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                return new DelegationDirective(
                    toAgent.GetString() ?? "",
                    msg,
                    question.GetString() ?? "");
            }
        }
        catch (JsonException) { }

        return null;
    }

    private static string BuildPromptWithContext(
        Agent? agent,
        List<Agent> allAgents,
        List<Agent> connectedAgents,
        List<ThreadMessage> priorMessages,
        bool allowDelegation,
        string? consultationContext,
        Project? project)
    {
        var sb = new StringBuilder();

        // Company and client context
        var companyName = !string.IsNullOrWhiteSpace(project?.CompanyName) ? project.CompanyName : "the company";
        sb.AppendLine($"You are an employee of {companyName}, a software development company.");
        sb.AppendLine("The person sending you messages (\"User\") is the company's client. They have commissioned the project described below.");
        sb.AppendLine("The client typically communicates with the CEO, but may also speak directly with other team members when needed.");
        sb.AppendLine("Treat the client professionally — they are paying for your work. Deliver high-quality results, ask clarifying questions when requirements are unclear, and keep them informed of progress.");
        sb.AppendLine();

        if (agent != null && !string.IsNullOrWhiteSpace(agent.Persona))
        {
            sb.AppendLine("You are operating under the following persona:");
            sb.AppendLine(agent.Persona);
            sb.AppendLine();
        }

        if (project != null)
        {
            sb.AppendLine("=== PROJECT CONTEXT ===");
            sb.AppendLine($"You are working on the project: {project.Name}");
            sb.AppendLine($"Project description: {project.Description}");
            sb.AppendLine();
        }

        // Org chart context
        if (agent != null)
        {
            sb.AppendLine("=== ORGANISATIONAL STRUCTURE ===");

            if (agent.IsCeo)
            {
                sb.AppendLine("You are the CEO of this organisation. You oversee all operations and set strategic direction.");
                sb.AppendLine("The client will primarily communicate with you. It is your responsibility to understand their requirements, break work down, and delegate to your team.");
            }

            // Manager info
            if (!string.IsNullOrEmpty(agent.ReportsToId))
            {
                var manager = allAgents.FirstOrDefault(a => a.Id == agent.ReportsToId);
                if (manager != null)
                {
                    sb.AppendLine($"You report to: {manager.Name} ({manager.JobTitle})");
                }
            }

            // Direct reports
            var directReports = allAgents.Where(a => a.ReportsToId == agent.Id).ToList();
            if (directReports.Count > 0)
            {
                sb.AppendLine("Your direct reports:");
                foreach (var report in directReports)
                {
                    var devLabel = report.IsDeveloper ? " [Developer]" : "";
                    sb.AppendLine($"  - {report.Name} ({report.JobTitle}){devLabel}");
                }
            }

            if (agent.IsDeveloper)
            {
                sb.AppendLine();
                sb.AppendLine("You are a DEVELOPER. Your current working directory is your personal workspace.");
                sb.AppendLine("When asked to develop, build, or implement something, you MUST write the code in your workspace directory.");
                sb.AppendLine("Create proper project structures, write clean code, and ensure everything builds correctly.");

                // Peer developer awareness
                var peerDevs = allAgents.Where(a => a.IsDeveloper && a.Id != agent.Id).ToList();
                if (peerDevs.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("PEER DEVELOPERS: You should be aware of other developers on the team:");
                    foreach (var peer in peerDevs)
                        sb.AppendLine($"  - {peer.Name} ({peer.JobTitle})");
                    sb.AppendLine("Use the shared directory (../shared/) to coordinate with peers — for example, agreeing on API contracts, sharing interface definitions, or leaving notes about integration points.");
                    sb.AppendLine("If you need to align with another developer (e.g., a frontend developer agreeing on an API contract with a backend developer), write the shared specification to the shared directory so both parties can reference it.");
                }
            }

            sb.AppendLine();
            sb.AppendLine("COMMUNICATION RULES: You may ONLY communicate with your direct manager and your direct reports. You cannot contact agents outside your reporting line.");
            sb.AppendLine("=== END ORGANISATIONAL STRUCTURE ===");
            sb.AppendLine();
        }

        if (project != null)
        {
            sb.AppendLine("SHARED DIRECTORY: There is a shared directory for this project where team members can exchange files (specs, notes, documentation).");
            sb.AppendLine("Path: ../shared/ (relative to your working directory)");
            sb.AppendLine("=== END PROJECT CONTEXT ===");
            sb.AppendLine();
        }

        if (priorMessages.Count == 1)
        {
            sb.AppendLine(priorMessages[0].Content);
        }
        else if (priorMessages.Count > 1)
        {
            sb.AppendLine("Conversation history:");
            sb.AppendLine();

            foreach (var msg in priorMessages)
            {
                var sender = msg.Direction == MessageDirection.Outbound ? "User" : (msg.FromAgentName ?? agent?.Name ?? "Agent");
                sb.AppendLine($"[{sender} ({msg.MessageId})]:");
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }

            sb.AppendLine("Please respond to the latest message, taking the full conversation history into account.");
        }

        if (!string.IsNullOrWhiteSpace(consultationContext))
        {
            sb.AppendLine();
            sb.AppendLine("You consulted with a colleague to help answer this question. Here is the exchange:");
            sb.AppendLine(consultationContext);
            sb.AppendLine("Now provide your final response to the user, incorporating what you learned from the consultation.");
        }

        if (allowDelegation && connectedAgents.Count > 0 && agent != null)
        {
            sb.AppendLine();
            sb.AppendLine("Team members you can delegate to (your direct reports and manager):");
            foreach (var other in connectedAgents)
            {
                var devLabel = other.IsDeveloper ? " [Developer - can write code]" : "";
                sb.AppendLine($"- {other.Name} ({other.JobTitle}){devLabel}");
            }
            sb.AppendLine();
            sb.AppendLine("If a task should be handled by one of these team members, delegate by responding with ONLY this JSON (no other text):");
            sb.AppendLine("""{"action": "delegate", "toAgent": "<name>", "question": "<the task or question with full context and specifications>"}""");
            sb.AppendLine("Only delegate when the task is clearly within another team member's responsibilities. If you already have consultation results above, use them to answer directly.");
            sb.AppendLine("When delegating development work to a developer, include complete specifications so they can implement it in their workspace.");
        }

        return sb.ToString();
    }

    public static string GenerateId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 5).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private record QueueItem(
        string AgentId,
        string ThreadId,
        int MessageNumber,
        bool IsConsultation = false,
        string? ConsultationContext = null,
        int DelegationDepth = 0);

    private record DelegationDirective(string ToAgent, string Message, string Question);

    private record ConsultationCallback(
        string OriginAgentId,
        string OriginThreadId,
        int OriginMessageNumber,
        string AccumulatedContext,
        int Depth);
}
