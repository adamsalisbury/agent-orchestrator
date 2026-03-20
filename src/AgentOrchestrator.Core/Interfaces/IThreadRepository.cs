using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Interfaces;

public interface IThreadRepository
{
    Task SaveMessageAsync(string agentId, ThreadMessage message);
    Task<ThreadMessage?> GetMessageAsync(string agentId, string threadId, int messageNumber);
    Task<List<ThreadMessage>> GetThreadMessagesAsync(string agentId, string threadId);
    Task<List<ThreadMessage>> GetAllMessagesAsync(string agentId);
}
