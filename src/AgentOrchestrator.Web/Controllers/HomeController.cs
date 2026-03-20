using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Web.Models;

namespace AgentOrchestrator.Web.Controllers;

public class HomeController : Controller
{
    private readonly IProjectRepository _projectRepo;

    public HomeController(IProjectRepository projectRepo)
    {
        _projectRepo = projectRepo;
    }

    public async Task<IActionResult> Index()
    {
        var project = await _projectRepo.GetAsync();
        if (project == null)
            return RedirectToAction("Setup", "Project");

        return RedirectToAction("Index", "Project");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
