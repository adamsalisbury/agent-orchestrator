namespace AgentOrchestrator.Core.Models;

public class ThreadMessage
{
    public string ThreadId { get; set; } = string.Empty;
    public int MessageNumber { get; set; }
    public string MessageId => $"{ThreadId}-{MessageNumber}";
    public MessageDirection Direction { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public MessageStatus Status { get; set; } = MessageStatus.Completed;

    // Set when a consulting agent responds in another agent's thread
    public string? FromAgentId { get; set; }
    public string? FromAgentName { get; set; }

}

public enum MessageDirection
{
    Outbound,
    Inbound
}

public enum MessageStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
