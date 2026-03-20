using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Interfaces;

public interface IClaudeRequestRepository
{
    Task<ClaudeRequest> GetByIdAsync(Guid id);
    Task<IReadOnlyList<ClaudeRequest>> GetAllAsync();
    Task AddAsync(ClaudeRequest request);
    Task UpdateAsync(ClaudeRequest request);
}
