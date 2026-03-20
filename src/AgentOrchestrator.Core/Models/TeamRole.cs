namespace AgentOrchestrator.Core.Models;

public class TeamRole
{
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? ReportsTo { get; set; }
    public bool IsDeveloper { get; set; }
}
