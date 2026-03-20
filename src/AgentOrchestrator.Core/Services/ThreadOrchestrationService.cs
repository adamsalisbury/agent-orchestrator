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

                // All agents work in the shared project workspace
                var workingDir = _projectRepository.GetWorkspacePath();

                // Get connected agents (manager + direct reports) for delegation
                var connectedAgents = GetConnectedAgents(agent, teamAgents);

                bool hasConsultationResults = !string.IsNullOrWhiteSpace(item.ConsultationContext);
                bool allowDelegation = !item.IsConsultation && !hasConsultationResults && item.DelegationDepth < MaxDelegationDepth && connectedAgents.Count > 0;
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
                sb.AppendLine("The client will primarily communicate with you. It is your responsibility to understand their requirements, break them down into tasks, and DELEGATE those tasks to your direct reports.");
                sb.AppendLine("CRITICAL: You do NOT do technical work yourself. You do NOT write code, design UIs, or configure infrastructure. You MUST delegate all work to the appropriate team members.");
                sb.AppendLine("When the client asks for something to be built, immediately delegate the work. Break it into appropriate tasks for your reports. Do not describe what you *would* do — actually delegate by using the delegation JSON format below.");
                sb.AppendLine("Once the project is built and running, you MUST report back to the client with the URL where they can see the finished product.");
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
                var isDevOps = IsDevOpsRole(agent.JobTitle);

                sb.AppendLine();
                sb.AppendLine("=== WORKSPACE ===");
                sb.AppendLine("Your current working directory is the shared project workspace. All developers and DevOps engineers work in this same directory.");
                sb.AppendLine("This means you can see code written by other team members and they can see yours.");

                if (isDevOps)
                {
                    sb.AppendLine();
                    sb.AppendLine("You are a DEVOPS ENGINEER. Your responsibilities include:");
                    sb.AppendLine("- Running and hosting the project (e.g., dotnet run, npx http-server, python -m http.server, or whatever is appropriate for the tech stack)");
                    sb.AppendLine("- Setting up build scripts, configuration, and deployment");
                    sb.AppendLine("- When a developer tells you the code is ready, review the workspace, run the project, and report back the URL where it can be accessed");
                    sb.AppendLine("- The application is hosted on this machine, so use localhost or 0.0.0.0 with an appropriate port");
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("You are a DEVELOPER. When asked to develop, build, or implement something, you MUST write the code in the workspace directory.");
                    sb.AppendLine("Create proper project structures, write clean code, and ensure everything builds correctly.");
                    sb.AppendLine("Once your work is complete, notify your manager or the DevOps engineer (via delegation) so they can run and host the project.");
                }

                // Peer awareness
                var peers = allAgents.Where(a => a.IsDeveloper && a.Id != agent.Id).ToList();
                if (peers.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("OTHER DEVELOPERS & DEVOPS IN THE TEAM:");
                    foreach (var peer in peers)
                        sb.AppendLine($"  - {peer.Name} ({peer.JobTitle})");
                    sb.AppendLine("Since you all share the same workspace, coordinate carefully. Use the shared directory (../shared/) for specifications, API contracts, and integration notes so everyone stays aligned.");
                }

                sb.AppendLine("=== END WORKSPACE ===");
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
            sb.AppendLine("=== COMPLETED DELEGATION RESULTS ===");
            sb.AppendLine("You previously delegated work to a team member. That work is now COMPLETE. Here is the exchange:");
            sb.AppendLine(consultationContext);
            sb.AppendLine("IMPORTANT: The delegated work has been done. Do NOT re-delegate the same task.");
            sb.AppendLine("Summarise what was accomplished and provide your final response to the person who asked you. If the work involved code, confirm it has been written. If you need to report upward, do so now.");
            sb.AppendLine("=== END DELEGATION RESULTS ===");
        }

        if (allowDelegation && connectedAgents.Count > 0 && agent != null)
        {
            sb.AppendLine();
            sb.AppendLine("=== DELEGATION ===");
            sb.AppendLine("Team members you can delegate to:");
            foreach (var other in connectedAgents)
            {
                var devLabel = other.IsDeveloper ? " [Developer - can write code]" : "";
                sb.AppendLine($"  - {other.Name} ({other.JobTitle}){devLabel}");
            }
            sb.AppendLine();
            sb.AppendLine("To delegate, respond with ONLY this JSON and nothing else:");
            sb.AppendLine("""{"action": "delegate", "toAgent": "<name>", "question": "<the task or question with full context and specifications>"}""");
            sb.AppendLine();

            if (agent.IsCeo)
            {
                sb.AppendLine("As CEO, you MUST delegate work rather than doing it yourself. When the client requests something, your job is to delegate immediately — do not explain what you plan to do, just delegate.");
                sb.AppendLine("You can only delegate to ONE person at a time. Start with the most senior or most relevant report for the task.");
            }
            else if (!agent.IsDeveloper)
            {
                sb.AppendLine("As a manager, if the request involves work that your reports should handle, delegate it to them with clear specifications.");
            }

            sb.AppendLine("When delegating development work, include complete specifications so the developer can implement it without further clarification.");
            sb.AppendLine("=== END DELEGATION ===");
        }

        return sb.ToString();
    }

    private static bool IsDevOpsRole(string jobTitle)
    {
        var keywords = new[] { "devops", "sre", "infrastructure", "platform engineer", "release", "operations" };
        var lower = jobTitle.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
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
