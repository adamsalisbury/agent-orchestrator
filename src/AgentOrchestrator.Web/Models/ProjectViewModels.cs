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

public class SetupAgentsViewModel
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDescription { get; set; } = string.Empty;
    public List<AgentViewModel> Agents { get; set; } = new();
}

public class GenerateTeamRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDescription { get; set; } = string.Empty;
}

public class CreateAgentFromRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? ReportsTo { get; set; }
    public bool IsDeveloper { get; set; }
    public bool IsCeo { get; set; }
}
