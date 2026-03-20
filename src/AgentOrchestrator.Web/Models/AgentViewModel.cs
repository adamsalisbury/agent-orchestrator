namespace AgentOrchestrator.Web.Models;

public class AgentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public bool IsDeveloper { get; set; }
    public bool IsCeo { get; set; }
    public string? ReportsToId { get; set; }
    public string? ReportsToName { get; set; }
    public List<string> DirectReportIds { get; set; } = new();
    public List<string> DirectReportNames { get; set; } = new();
    public bool IsBusy { get; set; }
    public string? CurrentTask { get; set; }
    public string? BlockedByAgentId { get; set; }
    public string? BlockedByAgentName { get; set; }
    public bool IsBlocked => !string.IsNullOrEmpty(BlockedByAgentId);
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

public class WorkspaceViewModel
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string CurrentPath { get; set; } = string.Empty;
    public List<WorkspaceEntry> Entries { get; set; } = new();
    public string? FileContent { get; set; }
    public string? FileName { get; set; }
    public bool IsViewingFile { get; set; }
}

public class WorkspaceEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
}
