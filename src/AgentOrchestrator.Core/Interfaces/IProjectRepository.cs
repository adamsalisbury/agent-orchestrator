using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetAsync();
    Task SaveAsync(Project project);
    Task DeleteAsync();
    string GetWorkspacePath();
    string GetSharedPath();
}
