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
                var workspaceDir = _projectRepository.GetWorkspacePath();

                bool allowDelegation = !item.IsConsultation && item.DelegationDepth < MaxDelegationDepth;
                var prompt = BuildPromptWithContext(agent, teamAgents, prior, allowDelegation, item.ConsultationContext, project);
                var response = await _runner.ExecuteAsync(prompt, workspaceDir);

                var delegation = allowDelegation ? TryParseDelegation(response) : null;

                if (delegation != null)
                {
                    var handled = await HandleDelegation(delegation, agent, teamAgents, item);
                    if (handled)
                        continue;
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

            var callbackKey = $"{item.AgentId}/{item.ThreadId}/{item.MessageNumber}";
            if (_callbacks.TryRemove(callbackKey, out var callback))
            {
                await HandleConsultationComplete(callback, message, item);
            }
        }
    }

    private async Task<bool> HandleDelegation(
        DelegationDirective delegation,
        Agent? sourceAgent,
        List<Agent> teamAgents,
        QueueItem item)
    {
        var targetAgent = teamAgents.FirstOrDefault(a =>
            a.Name.Equals(delegation.ToAgent, StringComparison.OrdinalIgnoreCase));

        if (targetAgent == null)
            return false;

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
        List<ThreadMessage> priorMessages,
        bool allowDelegation,
        string? consultationContext,
        Project? project)
    {
        var sb = new StringBuilder();

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

            var teammates = allAgents.Where(a => a.Id != agent?.Id).ToList();
            if (teammates.Count > 0)
            {
                sb.AppendLine("Your project team members:");
                foreach (var t in teammates)
                    sb.AppendLine($"- {t.Name} ({t.JobTitle})");
                sb.AppendLine();
            }

            sb.AppendLine("SHARED DIRECTORY: There is a shared directory for this project where team members can exchange files (specs, notes, documentation).");
            sb.AppendLine("Path: ../shared/ (relative to your working directory)");
            sb.AppendLine();
            sb.AppendLine("CODE WORKSPACE: Your current working directory is the project's code workspace. All code for this project should be written and managed here.");
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

        if (allowDelegation && allAgents.Count > 1 && agent != null)
        {
            var others = allAgents.Where(a => a.Id != agent.Id).ToList();
            if (others.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Available team members you can consult:");
                foreach (var other in others)
                {
                    sb.AppendLine($"- {other.Name} ({other.JobTitle})");
                }
                sb.AppendLine();
                sb.AppendLine("If a question is outside your expertise and another team member is better suited, you may delegate by responding with ONLY this JSON (no other text):");
                sb.AppendLine("""{"action": "delegate", "toAgent": "<name>", "question": "<the question to ask the other agent, with full context>"}""");
                sb.AppendLine("Only delegate when the question is clearly outside your expertise. If you already have consultation results above, use them to answer directly.");
            }
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
