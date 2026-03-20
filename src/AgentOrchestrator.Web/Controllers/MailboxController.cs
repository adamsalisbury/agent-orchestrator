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
    private readonly IClaudeCodeRunner _runner;
    private readonly ThreadOrchestrationService _threadService;
    private readonly PendingMessageTracker _tracker;

    public MailboxController(
        IAgentRepository agentRepo,
        IClaudeCodeRunner runner,
        ThreadOrchestrationService threadService,
        PendingMessageTracker tracker)
    {
        _agentRepo = agentRepo;
        _runner = runner;
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

        var skills = (model.Skills ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        await _agentRepo.CreateAsync(model.Name, model.JobTitle, model.Persona, skills);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> GeneratePersona([FromBody] GenerateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.JobTitle))
            return BadRequest(new { error = "Job title is required." });

        var purposeContext = string.IsNullOrWhiteSpace(request.Purpose)
            ? ""
            : $" Their purpose is: \"{request.Purpose}\".";

        var prompt = $"Generate an agent persona/system prompt for an AI agent whose job title is: \"{request.JobTitle}\".{purposeContext} " +
                     "Cover their expertise, work approach, and responsibilities. " +
                     "The agent will be part of a software development team collaborating with other specialist agents. " +
                     "Keep it under 100 words. Output ONLY the persona text, no preamble or explanation.";

        try
        {
            var result = await _runner.ExecuteAsync(prompt);
            return Json(new { persona = result.Trim() });
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

        var purposeContext = string.IsNullOrWhiteSpace(request.Purpose)
            ? ""
            : $" Their purpose is: \"{request.Purpose}\".";

        var prompt = $"List 5-8 short skill tags for an AI agent whose job title is: \"{request.JobTitle}\".{purposeContext} " +
                     "These are concise skill labels like \"UI/UX\", \"React\", \"API Design\", \"Code Review\". " +
                     "Output ONLY a comma-separated list, no numbering, no explanation.";

        try
        {
            var result = await _runner.ExecuteAsync(prompt);
            var skills = result.Trim()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"', '\''))
                .Where(s => s.Length > 0)
                .ToList();
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

    // --- Workspace Browser ---

    public async Task<IActionResult> Workspace(string id, string? path)
    {
        var agent = await _agentRepo.GetAsync(id);
        if (agent == null || !agent.IsDeveloper)
            return NotFound();

        var workspaceRoot = _agentRepo.GetAgentWorkspacePath(id);
        if (!Directory.Exists(workspaceRoot))
            Directory.CreateDirectory(workspaceRoot);

        // Sanitise path to prevent directory traversal
        var relativePath = SanitisePath(path ?? "");
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));

        // Ensure we don't escape the workspace root
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
            // Viewing a file
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

            // Still populate directory listing for the parent
            var parentDir = Path.GetDirectoryName(fullPath) ?? workspaceRoot;
            model.CurrentPath = Path.GetRelativePath(workspaceRoot, parentDir);
            if (model.CurrentPath == ".") model.CurrentPath = "";
            model.Entries = GetDirectoryEntries(parentDir, workspaceRoot);
        }
        else if (Directory.Exists(fullPath))
        {
            // Viewing a directory
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
                var name = Path.GetFileName(dir);
                entries.Add(new WorkspaceEntry
                {
                    Name = name,
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
        // Remove any directory traversal attempts
        return path.Replace("..", "").Replace("~", "").TrimStart('/').TrimStart('\\');
    }

    private static MessageRowViewModel BuildMessageRow(ThreadMessage m, Agent threadAgent, Dictionary<string, Agent> agentLookup, string? consultingAgentId = null)
    {
        string fromName, toName;
        string? fromAvatarId, toAvatarId;

        if (m.Direction == MessageDirection.Outbound && m.FromAgentId != null)
        {
            // Agent -> Agent consultation (e.g. Brian asking Sarah)
            var askingAgent = agentLookup.GetValueOrDefault(m.FromAgentId);
            fromName = askingAgent?.DisplayName ?? m.FromAgentName ?? "Unknown";
            fromAvatarId = m.FromAgentId;
            toName = threadAgent.DisplayName;
            toAvatarId = threadAgent.Id;
        }
        else if (m.Direction == MessageDirection.Outbound)
        {
            // User -> Agent
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
            // Agent -> User
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

            // Check agent flags for badge generation
            var agent = await _agentRepo.GetAsync(id);
            var svg = AvatarGenerator.Generate(id, agent?.IsDeveloper ?? false, agent?.IsCeo ?? false);
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
        Skills = a.Skills,
        CreatedAt = a.CreatedAt,
        IsDeveloper = a.IsDeveloper,
        IsCeo = a.IsCeo,
        ReportsToId = a.ReportsToId,
        ReportsToName = a.ReportsToName,
        DirectReportIds = a.DirectReportIds
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
