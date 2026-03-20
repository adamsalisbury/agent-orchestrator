using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Web.Controllers;

public class ProjectController : Controller
{
    private readonly IProjectRepository _projectRepo;
    private readonly IAgentRepository _agentRepo;

    public ProjectController(IProjectRepository projectRepo, IAgentRepository agentRepo)
    {
        _projectRepo = projectRepo;
        _agentRepo = agentRepo;
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
            Agents = agents.OrderBy(a => a.Name).Select(a => new AgentViewModel
            {
                Id = a.Id,
                Name = a.Name,
                JobTitle = a.JobTitle,
                Persona = a.Persona,
                Skills = a.Skills,
                CreatedAt = a.CreatedAt
            }).ToList(),
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
}
