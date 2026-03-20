namespace AgentOrchestrator.Web.Models;

public class AgentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string DisplayName => $"{Name} ({JobTitle})";
}

public class CreateAgentViewModel
{
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
}

public class GenerateAgentRequest
{
    public string JobTitle { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}
