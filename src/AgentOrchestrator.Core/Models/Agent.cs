namespace AgentOrchestrator.Core.Models;

public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string DisplayName => $"{Name} ({JobTitle})";
}
