using System.Text.Json;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Services;

public class TeamService
{
    private readonly IClaudeCodeRunner _runner;
    private readonly IAgentRepository _agentRepo;
    private readonly AgentService _agentService;

    public TeamService(IClaudeCodeRunner runner, IAgentRepository agentRepo, AgentService agentService)
    {
        _runner = runner;
        _agentRepo = agentRepo;
        _agentService = agentService;
    }

    public async Task<List<TeamRole>> GenerateTeamStructureAsync(string projectName, string projectDescription)
    {
        var prompt = Prompts.FormatTeamStructurePrompt(projectName, projectDescription);
        var result = await _runner.ExecuteAsync(prompt);

        var json = ExtractJsonArray(result);

        var roles = JsonSerializer.Deserialize<List<TeamRole>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return roles ?? new List<TeamRole>();
    }

    public async Task<Agent> CreateAgentFromRoleAsync(
        string name, string jobTitle, string purpose,
        string? reportsToName = null, bool isDeveloper = false, bool isCeo = false)
    {
        // Resolve reportsTo name to agent ID
        string? reportsToId = null;
        if (!string.IsNullOrWhiteSpace(reportsToName))
        {
            var allAgents = await _agentRepo.GetAllAsync();
            var manager = allAgents.FirstOrDefault(a =>
                a.Name.Equals(reportsToName, StringComparison.OrdinalIgnoreCase));
            reportsToId = manager?.Id;
        }

        // Detect CEO/developer from job title if not explicitly set
        isCeo = isCeo || AgentService.IsCeoRole(jobTitle);
        isDeveloper = isDeveloper || AgentService.IsDeveloperRole(jobTitle);

        // Generate persona and skills via Claude
        var persona = await _agentService.GeneratePersonaAsync(jobTitle, purpose, isDeveloper);
        var skills = await _agentService.GenerateSkillsAsync(jobTitle, purpose);

        // Create the agent
        return await _agentRepo.CreateAsync(
            name, jobTitle, persona, skills,
            isDeveloper, isCeo, reportsToId, reportsToName);
    }

    public static string ExtractJsonArray(string raw)
    {
        var json = raw.Trim();
        var start = json.IndexOf('[');
        var end = json.LastIndexOf(']');
        if (start >= 0 && end > start)
            return json[start..(end + 1)];
        return json;
    }
}
