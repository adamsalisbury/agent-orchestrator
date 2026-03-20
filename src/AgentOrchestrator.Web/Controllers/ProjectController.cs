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
            Name = project?.Name ?? "",
            Description = project?.Description ?? "",
            IsConfigured = project != null,
            Agents = agents.OrderBy(a => a.Name).Select(ToViewModel).ToList(),
            SharedFiles = sharedFiles
        });
    }

    public async Task<IActionResult> Edit()
    {
        var project = await _projectRepo.GetAsync();
        return View(new EditProjectViewModel
        {
            Name = project?.Name ?? "",
            Description = project?.Description ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProjectViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");

        if (!ModelState.IsValid)
            return View(model);

        await _projectRepo.SaveAsync(new Project
        {
            Name = model.Name,
            Description = model.Description ?? ""
        });

        return RedirectToAction(nameof(Index));
    }

    // --- Setup Wizard ---

    public async Task<IActionResult> Setup()
    {
        var project = await _projectRepo.GetAsync();
        return View(new EditProjectViewModel
        {
            Name = project?.Name ?? "",
            Description = project?.Description ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(EditProjectViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");

        if (!ModelState.IsValid)
            return View(model);

        await _projectRepo.SaveAsync(new Project
        {
            Name = model.Name,
            Description = model.Description ?? ""
        });

        return RedirectToAction(nameof(SetupAgents));
    }

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
}
