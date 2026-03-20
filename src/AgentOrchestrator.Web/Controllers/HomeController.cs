using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Web.Models;

namespace AgentOrchestrator.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IProjectRepository _projectRepo;

    public HomeController(ILogger<HomeController> logger, IProjectRepository projectRepo)
    {
        _logger = logger;
        _projectRepo = projectRepo;
    }

    public async Task<IActionResult> Index()
    {
        var project = await _projectRepo.GetAsync();
        if (project == null)
            return RedirectToAction("Setup", "Project");

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
