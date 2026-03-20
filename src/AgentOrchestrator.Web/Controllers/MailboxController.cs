using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Core.Services;
using AgentOrchestrator.Infrastructure.Services;
using AgentOrchestrator.Web.Models;
using AgentOrchestrator.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Web.Controllers;

public class MailboxController : Controller
{
    private readonly IAgentRepository _agentRepo;
    private readonly ThreadOrchestrationService _threadService;
    private readonly PendingMessageTracker _tracker;

    public MailboxController(
        IAgentRepository agentRepo,
        ThreadOrchestrationService threadService,
        PendingMessageTracker tracker)
    {
        _agentRepo = agentRepo;
        _threadService = threadService;
        _tracker = tracker;
    }

    // --- Agents ---

    public async Task<IActionResult> Index()
    {
        var agents = await _agentRepo.GetAllAsync();
        return View(agents.OrderBy(a => a.Name).Select(ToViewModel).ToList());
    }

    public IActionResult Create()
    {
        return View(new CreateAgentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAgentViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        if (string.IsNullOrWhiteSpace(model.JobTitle))
            ModelState.AddModelError(nameof(model.JobTitle), "Job title is required.");
        if (string.IsNullOrWhiteSpace(model.Persona))
            ModelState.AddModelError(nameof(model.Persona), "Persona is required.");

        if (!ModelState.IsValid)
            return View(model);

        await _agentRepo.CreateAsync(model.Name, model.JobTitle, model.Persona);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetEnvironment()
    {
        await _agentRepo.DeleteAllAsync();
        return RedirectToAction(nameof(Index));
    }

    // --- Requests (threads for an agent) ---

    public async Task<IActionResult> Requests(string id)
    {
        var agent = await _agentRepo.GetAsync(id);
        if (agent == null)
            return NotFound();

        var allMessages = await _threadService.GetAllMessagesAsync(id);

        var threads = allMessages
            .GroupBy(m => m.ThreadId)
            .Select(g =>
            {
                var messages = g.OrderBy(m => m.MessageNumber).ToList();
                var last = messages.Last();
                return new ThreadSummaryViewModel
                {
                    ThreadId = g.Key,
                    MessageCount = messages.Count,
                    LastMessagePreview = last.Status is MessageStatus.Pending or MessageStatus.Processing
                        ? $"Waiting for {agent.Name}..."
                        : (last.Content.Length > 100 ? last.Content[..100] + "..." : last.Content),
                    LastActivity = messages.Max(m => m.SentAt),
                    HasPending = messages.Any(m => m.Status is MessageStatus.Pending or MessageStatus.Processing)
                };
            })
            .OrderByDescending(t => t.LastActivity)
            .ToList();

        return View(new RequestsViewModel
        {
            AgentId = id,
            AgentName = agent.DisplayName,
            Threads = threads
        });
    }

    // --- Thread ---

    public async Task<IActionResult> Thread(string agentId, string threadId)
    {
        var agent = await _agentRepo.GetAsync(agentId);
        var messages = await _threadService.GetThreadMessagesAsync(agentId, threadId);
        return View(new ThreadViewModel
        {
            AgentId = agentId,
            AgentName = agent?.Name ?? agentId,
            ThreadId = threadId,
            Messages = messages.OrderBy(m => m.MessageNumber).Select(ToViewModel).ToList()
        });
    }

    // --- Compose ---

    public async Task<IActionResult> Compose(string? agentId)
    {
        var agents = await _agentRepo.GetAllAsync();
        return View(new ComposeViewModel
        {
            AgentId = agentId ?? string.Empty,
            AvailableAgents = agents.OrderBy(a => a.Name).Select(ToViewModel).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(ComposeViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.AgentId))
            ModelState.AddModelError(nameof(model.AgentId), "Please select an agent.");
        if (string.IsNullOrWhiteSpace(model.Content))
            ModelState.AddModelError(nameof(model.Content), "Message content is required.");

        if (!ModelState.IsValid)
        {
            var agents = await _agentRepo.GetAllAsync();
            model.AvailableAgents = agents.OrderBy(a => a.Name).Select(ToViewModel).ToList();
            return View(model);
        }

        var agent = await _agentRepo.GetAsync(model.AgentId);
        var result = await _threadService.SendMessageAsync(model.AgentId, null, model.Content);

        _tracker.Track(model.AgentId, agent?.Name ?? model.AgentId, result.ThreadId, result.MessageNumber + 1);

        return RedirectToAction(nameof(Thread), new { agentId = model.AgentId, threadId = result.ThreadId });
    }

    // --- Reply ---

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(string agentId, string threadId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return RedirectToAction(nameof(Thread), new { agentId, threadId });

        var agent = await _agentRepo.GetAsync(agentId);
        var result = await _threadService.SendMessageAsync(agentId, threadId, content);

        _tracker.Track(agentId, agent?.Name ?? agentId, threadId, result.MessageNumber + 1);

        return RedirectToAction(nameof(Thread), new { agentId, threadId });
    }

    // --- All Messages ---

    public async Task<IActionResult> AllMessages()
    {
        var agents = await _agentRepo.GetAllAsync();
        var agentLookup = agents.ToDictionary(a => a.Id);
        var rows = new List<MessageRowViewModel>();

        foreach (var agent in agents)
        {
            var messages = await _threadService.GetAllMessagesAsync(agent.Id);

            // For each thread, detect if it's a consultation thread (has outbound from another agent)
            var consultOriginByThread = messages
                .Where(m => m.Direction == MessageDirection.Outbound && m.FromAgentId != null)
                .GroupBy(m => m.ThreadId)
                .ToDictionary(g => g.Key, g => g.First().FromAgentId!);

            foreach (var m in messages)
            {
                consultOriginByThread.TryGetValue(m.ThreadId, out var consultingAgentId);
                var row = BuildMessageRow(m, agent, agentLookup, consultingAgentId);
                rows.Add(row);
            }
        }

        rows = rows.OrderByDescending(r => r.SentAtUtc).ToList();
        return View(new AllMessagesViewModel { Messages = rows });
    }

    public async Task<IActionResult> ViewMessage(string agentId, string threadId, int messageNumber)
    {
        var agents = await _agentRepo.GetAllAsync();
        var agentLookup = agents.ToDictionary(a => a.Id);
        var agent = agentLookup.GetValueOrDefault(agentId);
        if (agent == null) return NotFound();

        var messages = await _threadService.GetThreadMessagesAsync(agentId, threadId);
        var m = messages.FirstOrDefault(x => x.MessageNumber == messageNumber);
        if (m == null) return NotFound();

        // Check if this is a consultation thread
        var consultingAgentId = messages
            .FirstOrDefault(x => x.Direction == MessageDirection.Outbound && x.FromAgentId != null)
            ?.FromAgentId;
        var row = BuildMessageRow(m, agent, agentLookup, consultingAgentId);
        return View(new ViewMessageViewModel
        {
            AgentId = row.AgentId,
            ThreadId = row.ThreadId,
            MessageNumber = row.MessageNumber,
            MessageId = row.MessageId,
            SentAtUtc = row.SentAtUtc,
            Status = row.Status,
            FromName = row.FromName,
            FromAvatarId = row.FromAvatarId,
            ToName = row.ToName,
            ToAvatarId = row.ToAvatarId,
            Content = row.Content
        });
    }

    private static MessageRowViewModel BuildMessageRow(ThreadMessage m, Agent threadAgent, Dictionary<string, Agent> agentLookup, string? consultingAgentId = null)
    {
        string fromName, toName;
        string? fromAvatarId, toAvatarId;

        if (m.Direction == MessageDirection.Outbound && m.FromAgentId != null)
        {
            // Agent → Agent consultation (e.g. Brian asking Sarah)
            var askingAgent = agentLookup.GetValueOrDefault(m.FromAgentId);
            fromName = askingAgent?.DisplayName ?? m.FromAgentName ?? "Unknown";
            fromAvatarId = m.FromAgentId;
            toName = threadAgent.DisplayName;
            toAvatarId = threadAgent.Id;
        }
        else if (m.Direction == MessageDirection.Outbound)
        {
            // User → Agent
            fromName = "You";
            fromAvatarId = "adam";
            toName = threadAgent.DisplayName;
            toAvatarId = threadAgent.Id;
        }
        else if (m.FromAgentId != null)
        {
            // Consultation response in user's thread
            var consultAgent = agentLookup.GetValueOrDefault(m.FromAgentId);
            fromName = consultAgent?.DisplayName ?? m.FromAgentName ?? "Unknown";
            fromAvatarId = m.FromAgentId;
            toName = "You";
            toAvatarId = "adam";
        }
        else if (consultingAgentId != null)
        {
            // Agent responding in a consultation thread — reply goes back to the consulting agent
            var consultAgent = agentLookup.GetValueOrDefault(consultingAgentId);
            fromName = threadAgent.DisplayName;
            fromAvatarId = threadAgent.Id;
            toName = consultAgent?.DisplayName ?? "Unknown";
            toAvatarId = consultingAgentId;
        }
        else
        {
            // Agent → User
            fromName = threadAgent.DisplayName;
            fromAvatarId = threadAgent.Id;
            toName = "You";
            toAvatarId = "adam";
        }

        var preview = m.Status is MessageStatus.Pending or MessageStatus.Processing
            ? "Pending..."
            : (m.Content.Length > 80 ? m.Content[..80] + "..." : m.Content);

        return new MessageRowViewModel
        {
            AgentId = threadAgent.Id,
            ThreadId = m.ThreadId,
            MessageNumber = m.MessageNumber,
            MessageId = m.MessageId,
            SentAtUtc = m.SentAt,
            Status = m.Status.ToString(),
            FromName = fromName,
            FromAvatarId = fromAvatarId,
            ToName = toName,
            ToAvatarId = toAvatarId,
            ContentPreview = preview,
            Content = m.Content
        };
    }

    // --- Avatar ---

    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> Avatar(string id)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var avatarPath = Path.Combine(env.ContentRootPath, "App_Data", $"agent-{id}", "avatar.svg");

        if (!System.IO.File.Exists(avatarPath))
        {
            var agentDir = Path.GetDirectoryName(avatarPath)!;
            if (!Directory.Exists(agentDir))
                return NotFound();

            var svg = AvatarGenerator.Generate(id);
            await System.IO.File.WriteAllTextAsync(avatarPath, svg);
        }

        return PhysicalFile(avatarPath, "image/svg+xml");
    }

    // --- Mapping ---

    private static AgentViewModel ToViewModel(Agent a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        JobTitle = a.JobTitle,
        Persona = a.Persona,
        CreatedAt = a.CreatedAt
    };

    private static ThreadMessageViewModel ToViewModel(ThreadMessage m) => new()
    {
        ThreadId = m.ThreadId,
        MessageNumber = m.MessageNumber,
        MessageId = m.MessageId,
        Direction = m.Direction.ToString(),
        Content = m.Content,
        SentAt = m.SentAt,
        Status = m.Status.ToString(),
        FromAgentId = m.FromAgentId,
        FromAgentName = m.FromAgentName
    };
}
