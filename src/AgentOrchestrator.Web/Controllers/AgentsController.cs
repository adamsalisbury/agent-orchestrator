using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Core.Services;
using AgentOrchestrator.Infrastructure.Services;
using AgentOrchestrator.Web.Models;
using AgentOrchestrator.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Web.Controllers;

public class AgentsController : Controller
{
    private readonly IAgentRepository _agentRepo;
    private readonly AgentService _agentService;
    private readonly ThreadOrchestrationService _threadService;
    private readonly PendingMessageTracker _tracker;

    public AgentsController(
        IAgentRepository agentRepo,
        AgentService agentService,
        ThreadOrchestrationService threadService,
        PendingMessageTracker tracker)
    {
        _agentRepo = agentRepo;
        _agentService = agentService;
        _threadService = threadService;
        _tracker = tracker;
    }

    // --- Agents ---

    public async Task<IActionResult> Index()
    {
        var agents = await _agentRepo.GetAllAsync();
        return View(agents.OrderBy(a => a.Name).Select(a => ToViewModel(a, agents)).ToList());
    }

    public async Task<IActionResult> Detail(string id)
    {
        var agents = await _agentRepo.GetAllAsync();
        var agent = agents.FirstOrDefault(a => a.Id == id);
        if (agent == null)
            return NotFound();

        return View(ToViewModel(agent, agents));
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

        var skills = AgentService.ParseSkillsCsv(model.Skills ?? "");
        await _agentRepo.CreateAsync(model.Name, model.JobTitle, model.Persona, skills);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> GeneratePersona([FromBody] GenerateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.JobTitle))
            return BadRequest(new { error = "Job title is required." });

        try
        {
            var persona = await _agentService.GeneratePersonaAsync(request.JobTitle, request.Purpose);
            return Json(new { persona });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GenerateSkills([FromBody] GenerateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.JobTitle))
            return BadRequest(new { error = "Job title is required." });

        try
        {
            var skills = await _agentService.GenerateSkillsAsync(request.JobTitle, request.Purpose);
            return Json(new { skills });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetEnvironment()
    {
        await _agentRepo.DeleteAllAsync();
        return RedirectToAction(nameof(Index));
    }

    // --- Threads ---

    public async Task<IActionResult> Threads(string id)
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

        return View(new AgentThreadsViewModel
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
            Messages = messages.OrderBy(m => m.MessageNumber).Select(ToMessageViewModel).ToList()
        });
    }

    // --- Compose ---

    public async Task<IActionResult> Compose(string? agentId)
    {
        var agents = await _agentRepo.GetAllAsync();
        return View(new ComposeViewModel
        {
            AgentId = agentId ?? string.Empty,
            AvailableAgents = agents.OrderBy(a => a.Name).Select(a => ToViewModel(a)).ToList()
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
            model.AvailableAgents = agents.OrderBy(a => a.Name).Select(a => ToViewModel(a)).ToList();
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

    // --- Messages ---

    public async Task<IActionResult> Messages()
    {
        var agents = await _agentRepo.GetAllAsync();
        var agentLookup = agents.ToDictionary(a => a.Id);
        var rows = new List<MessageRowViewModel>();

        foreach (var agent in agents)
        {
            var messages = await _threadService.GetAllMessagesAsync(agent.Id);

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
        return View(new MessagesViewModel { Messages = rows });
    }

    public async Task<IActionResult> Message(string agentId, string threadId, int messageNumber)
    {
        var agents = await _agentRepo.GetAllAsync();
        var agentLookup = agents.ToDictionary(a => a.Id);
        var agent = agentLookup.GetValueOrDefault(agentId);
        if (agent == null) return NotFound();

        var messages = await _threadService.GetThreadMessagesAsync(agentId, threadId);
        var m = messages.FirstOrDefault(x => x.MessageNumber == messageNumber);
        if (m == null) return NotFound();

        var consultingAgentId = messages
            .FirstOrDefault(x => x.Direction == MessageDirection.Outbound && x.FromAgentId != null)
            ?.FromAgentId;
        var row = BuildMessageRow(m, agent, agentLookup, consultingAgentId);
        return View(new MessageDetailViewModel
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

    // --- Workspace Browser ---

    public async Task<IActionResult> Workspace(string id, string? path)
    {
        var agent = await _agentRepo.GetAsync(id);
        if (agent == null || !agent.IsDeveloper)
            return NotFound();

        var workspaceRoot = _agentRepo.GetAgentWorkspacePath(id);
        if (!Directory.Exists(workspaceRoot))
            Directory.CreateDirectory(workspaceRoot);

        var relativePath = SanitisePath(path ?? "");
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));

        if (!fullPath.StartsWith(Path.GetFullPath(workspaceRoot)))
            fullPath = workspaceRoot;

        var model = new WorkspaceViewModel
        {
            AgentId = id,
            AgentName = agent.DisplayName,
            CurrentPath = relativePath
        };

        if (System.IO.File.Exists(fullPath))
        {
            model.IsViewingFile = true;
            model.FileName = Path.GetFileName(fullPath);

            try
            {
                model.FileContent = await System.IO.File.ReadAllTextAsync(fullPath);
            }
            catch
            {
                model.FileContent = "[Binary file — cannot display]";
            }

            var parentDir = Path.GetDirectoryName(fullPath) ?? workspaceRoot;
            model.CurrentPath = Path.GetRelativePath(workspaceRoot, parentDir);
            if (model.CurrentPath == ".") model.CurrentPath = "";
            model.Entries = GetDirectoryEntries(parentDir, workspaceRoot);
        }
        else if (Directory.Exists(fullPath))
        {
            model.Entries = GetDirectoryEntries(fullPath, workspaceRoot);
        }

        return View(model);
    }

    private static List<WorkspaceEntry> GetDirectoryEntries(string directory, string workspaceRoot)
    {
        var entries = new List<WorkspaceEntry>();

        try
        {
            foreach (var dir in Directory.GetDirectories(directory).OrderBy(d => d))
            {
                entries.Add(new WorkspaceEntry
                {
                    Name = Path.GetFileName(dir),
                    IsDirectory = true,
                    RelativePath = Path.GetRelativePath(workspaceRoot, dir)
                });
            }

            foreach (var file in Directory.GetFiles(directory).OrderBy(f => f))
            {
                var info = new FileInfo(file);
                entries.Add(new WorkspaceEntry
                {
                    Name = info.Name,
                    IsDirectory = false,
                    RelativePath = Path.GetRelativePath(workspaceRoot, file),
                    Size = info.Length
                });
            }
        }
        catch { }

        return entries;
    }

    private static string SanitisePath(string path)
    {
        return path.Replace("..", "").Replace("~", "").TrimStart('/').TrimStart('\\');
    }

    private static MessageRowViewModel BuildMessageRow(ThreadMessage m, Agent threadAgent, Dictionary<string, Agent> agentLookup, string? consultingAgentId = null)
    {
        string fromName, toName;
        string? fromAvatarId, toAvatarId;

        if (m.Direction == MessageDirection.Outbound && m.FromAgentId != null)
        {
            var askingAgent = agentLookup.GetValueOrDefault(m.FromAgentId);
            fromName = askingAgent?.DisplayName ?? m.FromAgentName ?? "Unknown";
            fromAvatarId = m.FromAgentId;
            toName = threadAgent.DisplayName;
            toAvatarId = threadAgent.Id;
        }
        else if (m.Direction == MessageDirection.Outbound)
        {
            fromName = "You";
            fromAvatarId = "adam";
            toName = threadAgent.DisplayName;
            toAvatarId = threadAgent.Id;
        }
        else if (m.FromAgentId != null)
        {
            var consultAgent = agentLookup.GetValueOrDefault(m.FromAgentId);
            fromName = consultAgent?.DisplayName ?? m.FromAgentName ?? "Unknown";
            fromAvatarId = m.FromAgentId;
            toName = "You";
            toAvatarId = "adam";
        }
        else if (consultingAgentId != null)
        {
            var consultAgent = agentLookup.GetValueOrDefault(consultingAgentId);
            fromName = threadAgent.DisplayName;
            fromAvatarId = threadAgent.Id;
            toName = consultAgent?.DisplayName ?? "Unknown";
            toAvatarId = consultingAgentId;
        }
        else
        {
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

            var agent = await _agentRepo.GetAsync(id);
            var svg = AvatarGenerator.Generate(id, agent?.IsDeveloper ?? false, agent?.IsCeo ?? false);
            await System.IO.File.WriteAllTextAsync(avatarPath, svg);
        }

        return PhysicalFile(avatarPath, "image/svg+xml");
    }

    // --- Mapping ---

    private static AgentViewModel ToViewModel(Agent a, List<Agent>? allAgents = null) => new()
    {
        Id = a.Id,
        Name = a.Name,
        JobTitle = a.JobTitle,
        Persona = a.Persona,
        Skills = a.Skills,
        CreatedAt = a.CreatedAt,
        IsDeveloper = a.IsDeveloper,
        IsCeo = a.IsCeo,
        ReportsToId = a.ReportsToId,
        ReportsToName = a.ReportsToName,
        DirectReportIds = a.DirectReportIds,
        DirectReportNames = allAgents?
            .Where(r => r.ReportsToId == a.Id)
            .Select(r => $"{r.Name} ({r.JobTitle})")
            .ToList() ?? new(),
        IsBusy = a.IsBusy,
        CurrentTask = a.CurrentTask,
        BlockedByAgentId = a.BlockedByAgentId,
        BlockedByAgentName = a.BlockedByAgentName
    };

    private static ThreadMessageViewModel ToMessageViewModel(ThreadMessage m) => new()
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
