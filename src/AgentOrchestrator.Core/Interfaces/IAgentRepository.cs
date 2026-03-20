using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Interfaces;

public interface IAgentRepository
{
    Task<Agent> CreateAsync(string name, string jobTitle, string persona, List<string>? skills = null,
        bool isDeveloper = false, bool isCeo = false, string? reportsToId = null, string? reportsToName = null);
    Task<Agent?> GetAsync(string agentId);
    Task<List<Agent>> GetAllAsync();
    Task DeleteAllAsync();
    string GetAgentWorkspacePath(string agentId);
}
