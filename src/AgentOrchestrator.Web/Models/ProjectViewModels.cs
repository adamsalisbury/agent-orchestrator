namespace AgentOrchestrator.Web.Models;

public class ProjectViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<AgentViewModel> Agents { get; set; } = new();
    public List<string> SharedFiles { get; set; } = new();
    public bool IsConfigured { get; set; }
}

public class EditProjectViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
