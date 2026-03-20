using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Core.Services;
using AgentOrchestrator.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Web.Controllers;

public class ProjectController : Controller
{
    private readonly IProjectRepository _projectRepo;
    private readonly IAgentRepository _agentRepo;
    private readonly TeamService _teamService;

    public ProjectController(IProjectRepository projectRepo, IAgentRepository agentRepo, TeamService teamService)
    {
        _projectRepo = projectRepo;
        _agentRepo = agentRepo;
        _teamService = teamService;
    }

    public async Task<IActionResult> Index()
    {
        var project = await _projectRepo.GetAsync();
        if (project == null)
            return RedirectToAction(nameof(Setup));

        var agents = await _agentRepo.GetAllAsync();

        var sharedPath = _projectRepo.GetSharedPath();
        var sharedFiles = new List<string>();
        if (Directory.Exists(sharedPath))
        {
            sharedFiles = Directory.GetFileSystemEntries(sharedPath)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Select(f => f!)
                .OrderBy(f => f)
                .Take(50)
                .ToList();
        }

        return View(new ProjectViewModel
        {
            CompanyName = project.CompanyName,
            Name = project.Name,
            Description = project.Description,
            IsConfigured = true,
            Agents = agents.OrderBy(a => a.Name).Select(ToViewModel).ToList(),
            SharedFiles = sharedFiles
        });
    }

    public async Task<IActionResult> Edit()
    {
        var project = await _projectRepo.GetAsync();
        return View(new EditProjectViewModel
        {
            CompanyName = project?.CompanyName ?? "",
            Name = project?.Name ?? "",
            Description = project?.Description ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProjectViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.CompanyName))
            ModelState.AddModelError(nameof(model.CompanyName), "Company name is required.");
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Project name is required.");

        if (!ModelState.IsValid)
            return View(model);

        await _projectRepo.SaveAsync(new Project
        {
            CompanyName = model.CompanyName,
            Name = model.Name,
            Description = model.Description ?? ""
        });

        return RedirectToAction(nameof(Index));
    }

    // --- Setup Wizard ---

    // Step 1: Company Name
    public async Task<IActionResult> Setup()
    {
        var project = await _projectRepo.GetAsync();
        return View(new EditProjectViewModel
        {
            CompanyName = project?.CompanyName ?? "",
            Name = project?.Name ?? "",
            Description = project?.Description ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(EditProjectViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.CompanyName))
            ModelState.AddModelError(nameof(model.CompanyName), "Company name is required.");

        if (!ModelState.IsValid)
            return View(model);

        // Save company name now; project details come in step 2
        var existing = await _projectRepo.GetAsync();
        await _projectRepo.SaveAsync(new Project
        {
            CompanyName = model.CompanyName,
            Name = existing?.Name ?? "",
            Description = existing?.Description ?? ""
        });

        return RedirectToAction(nameof(SetupProject));
    }

    // Step 2: Project Details
    public async Task<IActionResult> SetupProject()
    {
        var project = await _projectRepo.GetAsync();
        if (project == null)
            return RedirectToAction(nameof(Setup));

        return View(new EditProjectViewModel
        {
            CompanyName = project.CompanyName,
            Name = project.Name,
            Description = project.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupProject(EditProjectViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Project name is required.");

        if (!ModelState.IsValid)
        {
            var project = await _projectRepo.GetAsync();
            model.CompanyName = project?.CompanyName ?? "";
            return View(model);
        }

        var existing = await _projectRepo.GetAsync();
        await _projectRepo.SaveAsync(new Project
        {
            CompanyName = existing?.CompanyName ?? model.CompanyName,
            Name = model.Name,
            Description = model.Description ?? ""
        });

        return RedirectToAction(nameof(SetupAgents));
    }

    // Step 3: Build Team
    public async Task<IActionResult> SetupAgents()
    {
        var project = await _projectRepo.GetAsync();
        if (project == null)
            return RedirectToAction(nameof(Setup));

        var agents = await _agentRepo.GetAllAsync();

        return View(new SetupAgentsViewModel
        {
            ProjectName = project.Name,
            ProjectDescription = project.Description,
            Agents = agents.OrderBy(a => a.Name).Select(ToViewModel).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> GenerateTeamStructure([FromBody] GenerateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.ProjectName))
            return BadRequest(new { error = "Project name is required." });

        try
        {
            var roles = await _teamService.GenerateTeamStructureAsync(request.ProjectName, request.ProjectDescription);
            return Json(new { roles });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgentFromRole([FromBody] CreateAgentFromRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Name) || string.IsNullOrWhiteSpace(request?.JobTitle))
            return BadRequest(new { error = "Name and job title are required." });

        try
        {
            var agent = await _teamService.CreateAgentFromRoleAsync(
                request.Name, request.JobTitle, request.Purpose,
                request.ReportsTo, request.IsDeveloper, request.IsCeo);

            return Json(new
            {
                success = true,
                agent = new
                {
                    agent.Id,
                    agent.Name,
                    agent.JobTitle,
                    agent.Persona,
                    agent.Skills,
                    agent.IsDeveloper,
                    agent.IsCeo,
                    agent.ReportsToName
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // --- Workspace Browser ---

    public IActionResult Workspace(string? path)
    {
        var workspaceRoot = _projectRepo.GetWorkspacePath();
        if (!Directory.Exists(workspaceRoot))
            Directory.CreateDirectory(workspaceRoot);

        var relativePath = SanitisePath(path ?? "");
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));

        if (!fullPath.StartsWith(Path.GetFullPath(workspaceRoot)))
            fullPath = workspaceRoot;

        var model = new WorkspaceViewModel
        {
            AgentId = "",
            AgentName = "Project Workspace",
            CurrentPath = relativePath
        };

        if (System.IO.File.Exists(fullPath))
        {
            model.IsViewingFile = true;
            model.FileName = Path.GetFileName(fullPath);

            try
            {
                model.FileContent = System.IO.File.ReadAllText(fullPath);
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
        DirectReportIds = a.DirectReportIds,
        IsBusy = a.IsBusy,
        CurrentTask = a.CurrentTask,
        BlockedByAgentId = a.BlockedByAgentId,
        BlockedByAgentName = a.BlockedByAgentName
    };
}
