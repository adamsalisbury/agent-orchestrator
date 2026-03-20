namespace AgentOrchestrator.Core.Models;

public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeveloper { get; set; }
    public bool IsCeo { get; set; }
    public string? ReportsToId { get; set; }
    public string? ReportsToName { get; set; }
    public List<string> DirectReportIds { get; set; } = new();

    // Current task status (transient, not persisted in persona.md)
    public bool IsBusy { get; set; }
    public string? CurrentTask { get; set; }
    public string? BlockedByAgentId { get; set; }
    public string? BlockedByAgentName { get; set; }
    public bool IsBlocked => !string.IsNullOrEmpty(BlockedByAgentId);

    public string DisplayName => $"{Name} ({JobTitle})";
}
