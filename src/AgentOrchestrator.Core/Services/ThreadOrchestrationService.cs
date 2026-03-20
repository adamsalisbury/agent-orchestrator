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
    private readonly IClaudeCodeRunner _runner;
    private readonly Channel<QueueItem> _queue;
    private readonly ConcurrentDictionary<string, ConsultationCallback> _callbacks = new();

    public ThreadOrchestrationService(
        IThreadRepository threadRepository,
        IAgentRepository agentRepository,
        IClaudeCodeRunner runner)
    {
        _threadRepository = threadRepository;
        _agentRepository = agentRepository;
        _runner = runner;
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

            // Only set to Processing if not already (re-runs keep it Processing)
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
                var allAgents = await _agentRepository.GetAllAsync();

                bool allowDelegation = !item.IsConsultation && item.DelegationDepth < MaxDelegationDepth;
                var prompt = BuildPromptWithContext(agent, allAgents, prior, allowDelegation, item.ConsultationContext);
                var response = await _runner.ExecuteAsync(prompt);

                // Check for delegation
                var delegation = allowDelegation ? TryParseDelegation(response) : null;

                if (delegation != null)
                {
                    var handled = await HandleDelegation(delegation, agent, allAgents, item);
                    if (handled)
                        continue; // Don't save - message stays Processing until Brian re-runs
                }

                // Normal response (or delegation target not found)
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

            // Check if this completed message triggers a callback (consultation response)
            var callbackKey = $"{item.AgentId}/{item.ThreadId}/{item.MessageNumber}";
            if (_callbacks.TryRemove(callbackKey, out var callback))
            {
                await HandleConsultationComplete(callback, message, item);
            }
        }
    }

    /// <summary>
    /// Brian delegated to another agent. Create the consultation thread and queue it.
    /// Returns true if delegation was set up (message stays Processing).
    /// </summary>
    private async Task<bool> HandleDelegation(
        DelegationDirective delegation,
        Agent? sourceAgent,
        List<Agent> allAgents,
        QueueItem item)
    {
        var targetAgent = allAgents.FirstOrDefault(a =>
            a.Name.Equals(delegation.ToAgent, StringComparison.OrdinalIgnoreCase));

        if (targetAgent == null)
            return false; // Let the normal response path handle it

        // Build the accumulated consultation context
        var newContext = item.ConsultationContext ?? "";
        newContext += $"\n[You asked {targetAgent.Name} ({targetAgent.JobTitle})]: {delegation.Question}\n";

        // Create consultation thread in the TARGET agent's space
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

        // Store callback: when target responds, re-run origin agent with the answer
        _callbacks[$"{targetAgent.Id}/{consultThreadId}/2"] = new ConsultationCallback(
            item.AgentId,
            item.ThreadId,
            item.MessageNumber,
            newContext,
            item.DelegationDepth + 1);

        // Queue the target agent's response (consultation - no further delegation allowed)
        await _queue.Writer.WriteAsync(new QueueItem(
            targetAgent.Id, consultThreadId, 2,
            IsConsultation: true));

        return true;
    }

    /// <summary>
    /// A consulted agent (Sarah) has responded. Feed the answer back to the origin agent (Brian).
    /// </summary>
    private async Task HandleConsultationComplete(
        ConsultationCallback callback,
        ThreadMessage consultationResponse,
        QueueItem item)
    {
        var respondingAgent = await _agentRepository.GetAsync(item.AgentId);
        var respondingName = respondingAgent?.Name ?? item.AgentId;

        // Add the response to the accumulated context
        var updatedContext = callback.AccumulatedContext
            + $"[{respondingName} replied]: {consultationResponse.Content}\n";

        // Re-queue the ORIGIN agent to process again with the consultation result
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
        string? consultationContext)
    {
        var sb = new StringBuilder();

        if (agent != null && !string.IsNullOrWhiteSpace(agent.Persona))
        {
            sb.AppendLine("You are operating under the following persona:");
            sb.AppendLine(agent.Persona);
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

        // Include consultation results if the agent consulted a colleague
        if (!string.IsNullOrWhiteSpace(consultationContext))
        {
            sb.AppendLine();
            sb.AppendLine("You consulted with a colleague to help answer this question. Here is the exchange:");
            sb.AppendLine(consultationContext);
            sb.AppendLine("Now provide your final response to the user, incorporating what you learned from the consultation.");
        }

        // Agent roster and delegation instructions
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
