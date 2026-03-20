namespace AgentOrchestrator.Core.Models;

public class ClaudeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Prompt { get; set; } = string.Empty;
    public ClaudeRequestStatus Status { get; set; } = ClaudeRequestStatus.Pending;
    public string? Response { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ClaudeRequestStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
