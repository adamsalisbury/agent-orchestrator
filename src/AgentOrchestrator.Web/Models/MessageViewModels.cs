namespace AgentOrchestrator.Web.Models;

public class AgentThreadsViewModel
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public List<ThreadSummaryViewModel> Threads { get; set; } = new();
}

public class ThreadSummaryViewModel
{
    public string ThreadId { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public bool HasPending { get; set; }
}

public class ThreadViewModel
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public List<ThreadMessageViewModel> Messages { get; set; } = new();
    public bool HasPendingResponse => Messages.Any(m => m.IsPending);
}

public class ComposeViewModel
{
    public string AgentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AgentViewModel> AvailableAgents { get; set; } = new();
}

public class MessagesViewModel
{
    public List<MessageRowViewModel> Messages { get; set; } = new();
}

public class MessageRowViewModel
{
    public string AgentId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public int MessageNumber { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
    public string? FromAvatarId { get; set; }

    public string ToName { get; set; } = string.Empty;
    public string? ToAvatarId { get; set; }

    public string ContentPreview { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public bool IsPending => Status is "Pending" or "Processing";
}

public class MessageDetailViewModel
{
    public string AgentId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public int MessageNumber { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
    public string? FromAvatarId { get; set; }

    public string ToName { get; set; } = string.Empty;
    public string? ToAvatarId { get; set; }

    public string Content { get; set; } = string.Empty;
}
