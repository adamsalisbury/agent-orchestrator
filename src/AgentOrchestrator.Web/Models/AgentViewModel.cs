namespace AgentOrchestrator.Web.Models;

public class AgentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DisplayName => $"{Name} ({JobTitle})";
}

public class CreateAgentViewModel
{
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
}
