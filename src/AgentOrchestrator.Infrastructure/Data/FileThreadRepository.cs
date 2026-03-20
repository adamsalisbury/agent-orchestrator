using System.Globalization;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Infrastructure.Data;

public class FileThreadRepository : IThreadRepository
{
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileThreadRepository(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    private string GetThreadDir(string agentId, string threadId) =>
        Path.Combine(_baseDirectory, $"agent-{agentId}", "threads", $"thread-{threadId}");

    public async Task SaveMessageAsync(string agentId, ThreadMessage message)
    {
        await _lock.WaitAsync();
        try
        {
            var threadDir = GetThreadDir(agentId, message.ThreadId);
            Directory.CreateDirectory(threadDir);

            var filePath = Path.Combine(threadDir, $"{message.MessageId}.md");
            var content = FormatMessage(message);
            await File.WriteAllTextAsync(filePath, content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThreadMessage?> GetMessageAsync(string agentId, string threadId, int messageNumber)
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(GetThreadDir(agentId, threadId), $"{threadId}-{messageNumber}.md");
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllTextAsync(filePath);
            return ParseMessage(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ThreadMessage>> GetThreadMessagesAsync(string agentId, string threadId)
    {
        await _lock.WaitAsync();
        try
        {
            var threadDir = GetThreadDir(agentId, threadId);
            if (!Directory.Exists(threadDir))
                return new List<ThreadMessage>();

            var messages = new List<ThreadMessage>();
            foreach (var file in Directory.GetFiles(threadDir, "*.md").OrderBy(f => f))
            {
                var content = await File.ReadAllTextAsync(file);
                var message = ParseMessage(content);
                if (message != null)
                    messages.Add(message);
            }

            return messages.OrderBy(m => m.MessageNumber).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ThreadMessage>> GetAllMessagesAsync(string agentId)
    {
        await _lock.WaitAsync();
        try
        {
            var messages = new List<ThreadMessage>();
            var threadsDir = Path.Combine(_baseDirectory, $"agent-{agentId}", "threads");
            if (!Directory.Exists(threadsDir))
                return messages;

            foreach (var threadDir in Directory.GetDirectories(threadsDir, "thread-*"))
            {
                foreach (var file in Directory.GetFiles(threadDir, "*.md"))
                {
                    var content = await File.ReadAllTextAsync(file);
                    var message = ParseMessage(content);
                    if (message != null)
                        messages.Add(message);
                }
            }

            return messages;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string FormatMessage(ThreadMessage message)
    {
        var fm = $"---\nthreadId: {message.ThreadId}\nmessageNumber: {message.MessageNumber}\ndirection: {message.Direction}\nsentAt: {message.SentAt:O}\nstatus: {message.Status}";
        if (!string.IsNullOrEmpty(message.FromAgentId))
            fm += $"\nfromAgentId: {message.FromAgentId}";
        if (!string.IsNullOrEmpty(message.FromAgentName))
            fm += $"\nfromAgentName: {message.FromAgentName}";
        fm += $"\n---\n\n{message.Content}";
        return fm;
    }

    private static ThreadMessage? ParseMessage(string fileContent)
    {
        if (!fileContent.StartsWith("---"))
            return null;

        var endOfFrontmatter = fileContent.IndexOf("---", 3);
        if (endOfFrontmatter < 0)
            return null;

        var frontmatter = fileContent[3..endOfFrontmatter].Trim();
        var body = fileContent[(endOfFrontmatter + 3)..].Trim();

        var meta = new Dictionary<string, string>();
        foreach (var line in frontmatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();
                meta[key] = value;
            }
        }

        return new ThreadMessage
        {
            ThreadId = meta.GetValueOrDefault("threadId", ""),
            MessageNumber = int.TryParse(meta.GetValueOrDefault("messageNumber", "0"), out var num) ? num : 0,
            Direction = Enum.TryParse<MessageDirection>(meta.GetValueOrDefault("direction", ""), out var dir) ? dir : MessageDirection.Outbound,
            SentAt = DateTime.TryParse(meta.GetValueOrDefault("sentAt", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow,
            Status = Enum.TryParse<MessageStatus>(meta.GetValueOrDefault("status", ""), out var st) ? st : MessageStatus.Completed,
            FromAgentId = meta.TryGetValue("fromAgentId", out var fai) ? fai : null,
            FromAgentName = meta.TryGetValue("fromAgentName", out var fan) ? fan : null,
            Content = body
        };
    }
}
