using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Interfaces;

public interface IAgentRepository
{
    Task<Agent> CreateAsync(string name, string jobTitle, string persona, List<string>? skills = null);
    Task<Agent?> GetAsync(string agentId);
    Task<List<Agent>> GetAllAsync();
    Task DeleteAllAsync();
}
