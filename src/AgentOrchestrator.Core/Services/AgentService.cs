using AgentOrchestrator.Core.Interfaces;

namespace AgentOrchestrator.Core.Services;

public class AgentService
{
    private readonly IClaudeCodeRunner _runner;

    public AgentService(IClaudeCodeRunner runner)
    {
        _runner = runner;
    }

    public async Task<string> GeneratePersonaAsync(string jobTitle, string? purpose = null, bool isDeveloper = false)
    {
        var prompt = Prompts.FormatPersonaPrompt(jobTitle, purpose, isDeveloper);
        var result = await _runner.ExecuteAsync(prompt);
        return result.Trim();
    }

    public async Task<List<string>> GenerateSkillsAsync(string jobTitle, string? purpose = null)
    {
        var prompt = Prompts.FormatSkillsPrompt(jobTitle, purpose);
        var result = await _runner.ExecuteAsync(prompt);
        return ParseSkillsCsv(result);
    }

    public static List<string> ParseSkillsCsv(string raw)
    {
        return raw.Trim()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim('"', '\''))
            .Where(s => s.Length > 0)
            .ToList();
    }

    public static bool IsDeveloperRole(string jobTitle)
    {
        var keywords = new[] { "developer", "engineer", "programmer", "coder", "dev ",
            "frontend", "backend", "full stack", "fullstack", "full-stack", "devops", "sre" };
        var lower = jobTitle.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
    }

    public static bool IsCeoRole(string jobTitle)
    {
        return jobTitle.Contains("CEO", StringComparison.OrdinalIgnoreCase) ||
               jobTitle.Contains("Chief Executive", StringComparison.OrdinalIgnoreCase);
    }
}
