using System.Collections.Concurrent;

namespace AgentOrchestrator.Web.Services;

public record PendingMessage(string AgentId, string AgentName, string ThreadId, int MessageNumber)
{
    public string Key => $"{AgentId}/{ThreadId}/{MessageNumber}";
}

public class PendingMessageTracker
{
    private readonly ConcurrentDictionary<string, PendingMessage> _pending = new();

    public void Track(string agentId, string agentName, string threadId, int messageNumber)
    {
        var msg = new PendingMessage(agentId, agentName, threadId, messageNumber);
        _pending[msg.Key] = msg;
    }

    public IEnumerable<PendingMessage> GetAll() => _pending.Values;

    public void Remove(string key) => _pending.TryRemove(key, out _);
}
