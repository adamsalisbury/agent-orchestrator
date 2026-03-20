namespace AgentOrchestrator.Web.Models;

public class ThreadMessageViewModel
{
    public string ThreadId { get; set; } = string.Empty;
    public int MessageNumber { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Status { get; set; } = string.Empty;

    public string? FromAgentId { get; set; }
    public string? FromAgentName { get; set; }
    public bool IsConsultation => FromAgentName != null;

    public bool IsInbound => Direction == "Inbound";
    public bool IsOutbound => Direction == "Outbound";
    public bool IsPending => Status is "Pending" or "Processing";

    public string StatusBadgeClass => Status switch
    {
        "Pending" => "bg-secondary",
        "Processing" => "bg-warning text-dark",
        "Completed" => "bg-success",
        "Failed" => "bg-danger",
        _ => "bg-secondary"
    };
}
