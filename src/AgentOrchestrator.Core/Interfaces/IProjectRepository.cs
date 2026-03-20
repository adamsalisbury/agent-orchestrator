using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetAsync();
    Task SaveAsync(Project project);
    string GetWorkspacePath();
    string GetSharedPath();
}
