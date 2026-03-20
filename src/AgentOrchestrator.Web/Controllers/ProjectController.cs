using System.Text.Json;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Web.Controllers;

public class ProjectController : Controller
{
    private readonly IProjectRepository _projectRepo;
    private readonly IAgentRepository _agentRepo;
    private readonly IClaudeCodeRunner _runner;

    public ProjectController(IProjectRepository projectRepo, IAgentRepository agentRepo, IClaudeCodeRunner runner)
    {
        _projectRepo = projectRepo;
        _agentRepo = agentRepo;
        _runner = runner;
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
            Agents = agents.OrderBy(a => a.Name).Select(a => new AgentViewModel
            {
                Id = a.Id,
                Name = a.Name,
                JobTitle = a.JobTitle,
                Persona = a.Persona,
                Skills = a.Skills,
                CreatedAt = a.CreatedAt
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> GenerateTeamStructure([FromBody] GenerateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.ProjectName))
            return BadRequest(new { error = "Project name is required." });

        var prompt = $@"You are designing an organisational structure for a project team. The project is:

Project Name: ""{request.ProjectName}""
Project Description: ""{request.ProjectDescription}""

Design a team of between 5 and 10 people to work on this project. The team MUST include a CEO at the top, followed by the key roles needed for this specific type of project/product.

The org chart should be realistic and tailored to the project — for example, a SaaS product needs different roles than a mobile game or a data platform.

Each person needs a realistic first name, a job title, and a brief purpose statement describing their responsibilities.

Output ONLY valid JSON — no markdown fences, no explanation. Use this exact format:
[
  {{""name"": ""Alex"", ""jobTitle"": ""Chief Executive Officer"", ""purpose"": ""Oversees all operations, sets strategic direction, and ensures alignment across the team"", ""reportsTo"": null}},
  {{""name"": ""Sarah"", ""jobTitle"": ""VP of Engineering"", ""purpose"": ""Leads the technical team and architecture decisions"", ""reportsTo"": ""Alex""}}
]

The reportsTo field should contain the name of the person they report to, or null for the CEO.";

        try
        {
            var result = await _runner.ExecuteAsync(prompt);

            // Extract JSON from response (handle potential markdown fences)
            var json = result.Trim();
            if (json.Contains("```"))
            {
                var start = json.IndexOf('[');
                var end = json.LastIndexOf(']');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            var roles = JsonSerializer.Deserialize<List<TeamRoleDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
            // Generate persona
            var personaPrompt = $"Generate an agent persona/system prompt for an AI agent whose job title is: \"{request.JobTitle}\". " +
                                $"Their purpose is: \"{request.Purpose}\". " +
                                "Cover their expertise, work approach, and responsibilities. " +
                                "The agent will be part of a software development team collaborating with other specialist agents. " +
                                "Keep it under 100 words. Output ONLY the persona text, no preamble or explanation.";

            var persona = (await _runner.ExecuteAsync(personaPrompt)).Trim();

            // Generate skills
            var skillsPrompt = $"List 5-8 short skill tags for an AI agent whose job title is: \"{request.JobTitle}\". " +
                               $"Their purpose is: \"{request.Purpose}\". " +
                               "These are concise skill labels like \"UI/UX\", \"React\", \"API Design\", \"Code Review\". " +
                               "Output ONLY a comma-separated list, no numbering, no explanation.";

            var skillsResult = (await _runner.ExecuteAsync(skillsPrompt)).Trim();
            var skills = skillsResult
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"', '\''))
                .Where(s => s.Length > 0)
                .ToList();

            // Create the agent
            var agent = await _agentRepo.CreateAsync(request.Name, request.JobTitle, persona, skills);

            return Json(new
            {
                success = true,
                agent = new
                {
                    agent.Id,
                    agent.Name,
                    agent.JobTitle,
                    agent.Persona,
                    agent.Skills
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
