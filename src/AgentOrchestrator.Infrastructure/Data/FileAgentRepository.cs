using System.Globalization;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Core.Services;
using AgentOrchestrator.Infrastructure.Services;

namespace AgentOrchestrator.Infrastructure.Data;

public class FileAgentRepository : IAgentRepository
{
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileAgentRepository(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<Agent> CreateAsync(string name, string jobTitle, string persona, List<string>? skills = null,
        bool isDeveloper = false, bool isCeo = false, string? reportsToId = null, string? reportsToName = null)
    {
        await _lock.WaitAsync();
        try
        {
            var id = ThreadOrchestrationService.GenerateId();
            var agentDir = Path.Combine(_baseDirectory, $"agent-{id}");
            Directory.CreateDirectory(agentDir);

            var agent = new Agent
            {
                Id = id,
                Name = name,
                JobTitle = jobTitle,
                Persona = persona,
                Skills = skills ?? new(),
                CreatedAt = DateTime.UtcNow,
                IsDeveloper = isDeveloper,
                IsCeo = isCeo,
                ReportsToId = reportsToId,
                ReportsToName = reportsToName
            };

            var skillsCsv = string.Join(",", agent.Skills);
            var content = $"---\nagentId: {agent.Id}\nname: {agent.Name}\njobTitle: {agent.JobTitle}\nskills: {skillsCsv}\ncreatedAt: {agent.CreatedAt:O}\nisDeveloper: {agent.IsDeveloper}\nisCeo: {agent.IsCeo}\nreportsToId: {agent.ReportsToId ?? ""}\nreportsToName: {agent.ReportsToName ?? ""}\n---\n\n{agent.Persona}";
            await File.WriteAllTextAsync(Path.Combine(agentDir, "persona.md"), content);

            var avatarSvg = AvatarGenerator.Generate(id, isDeveloper, isCeo);
            await File.WriteAllTextAsync(Path.Combine(agentDir, "avatar.svg"), avatarSvg);

            return agent;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Agent?> GetAsync(string agentId)
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_baseDirectory, $"agent-{agentId}", "persona.md");
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllTextAsync(filePath);
            var agent = ParseAgent(content);
            if (agent != null)
                await LoadCurrentTask(agent);
            return agent;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Agent>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var agents = new List<Agent>();
            if (!Directory.Exists(_baseDirectory))
                return agents;

            foreach (var agentDir in Directory.GetDirectories(_baseDirectory, "agent-*"))
            {
                var filePath = Path.Combine(agentDir, "persona.md");
                if (!File.Exists(filePath))
                    continue;

                var content = await File.ReadAllTextAsync(filePath);
                var agent = ParseAgent(content);
                if (agent != null)
                {
                    await LoadCurrentTask(agent);
                    agents.Add(agent);
                }
            }

            // Populate DirectReportIds from ReportsToId relationships
            var agentLookup = agents.ToDictionary(a => a.Id);
            foreach (var agent in agents)
            {
                agent.DirectReportIds = agents
                    .Where(a => a.ReportsToId == agent.Id)
                    .Select(a => a.Id)
                    .ToList();
            }

            return agents;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!Directory.Exists(_baseDirectory))
                return;

            foreach (var agentDir in Directory.GetDirectories(_baseDirectory, "agent-*"))
            {
                var dirName = Path.GetFileName(agentDir);
                if (dirName == "agent-adam")
                    continue;
                Directory.Delete(agentDir, recursive: true);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public string GetAgentWorkspacePath(string agentId)
    {
        return Path.Combine(_baseDirectory, $"agent-{agentId}", "workspace");
    }

    public async Task SetCurrentTaskAsync(string agentId, string task, string? blockedByAgentId = null, string? blockedByAgentName = null)
    {
        var filePath = Path.Combine(_baseDirectory, $"agent-{agentId}", "current-task.md");
        var blockedId = blockedByAgentId ?? "";
        var blockedName = blockedByAgentName ?? "";
        var content = $"---\nstatus: {(string.IsNullOrEmpty(blockedByAgentId) ? "busy" : "blocked")}\nblockedByAgentId: {blockedId}\nblockedByAgentName: {blockedName}\n---\n\n{task}";
        await File.WriteAllTextAsync(filePath, content);
    }

    public Task ClearCurrentTaskAsync(string agentId)
    {
        var filePath = Path.Combine(_baseDirectory, $"agent-{agentId}", "current-task.md");
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    private async Task LoadCurrentTask(Agent agent)
    {
        var taskPath = Path.Combine(_baseDirectory, $"agent-{agent.Id}", "current-task.md");
        if (!File.Exists(taskPath))
            return;

        var content = await File.ReadAllTextAsync(taskPath);
        if (!content.StartsWith("---"))
            return;

        var endOfFrontmatter = content.IndexOf("---", 3);
        if (endOfFrontmatter < 0)
            return;

        var frontmatter = content[3..endOfFrontmatter].Trim();
        var body = content[(endOfFrontmatter + 3)..].Trim();

        var meta = new Dictionary<string, string>();
        foreach (var line in frontmatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();
                meta[key] = value;
            }
        }

        agent.IsBusy = true;
        agent.CurrentTask = body;
        var blockedId = meta.GetValueOrDefault("blockedByAgentId", "");
        agent.BlockedByAgentId = string.IsNullOrWhiteSpace(blockedId) ? null : blockedId;
        var blockedName = meta.GetValueOrDefault("blockedByAgentName", "");
        agent.BlockedByAgentName = string.IsNullOrWhiteSpace(blockedName) ? null : blockedName;
    }

    private static Agent? ParseAgent(string fileContent)
    {
        if (!fileContent.StartsWith("---"))
            return null;

        var endOfFrontmatter = fileContent.IndexOf("---", 3);
        if (endOfFrontmatter < 0)
            return null;

        var frontmatter = fileContent[3..endOfFrontmatter].Trim();
        var body = fileContent[(endOfFrontmatter + 3)..].Trim();

        var meta = new Dictionary<string, string>();
        foreach (var line in frontmatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();
                meta[key] = value;
            }
        }

        return new Agent
        {
            Id = meta.GetValueOrDefault("agentId", ""),
            Name = meta.GetValueOrDefault("name", ""),
            JobTitle = meta.GetValueOrDefault("jobTitle", ""),
            Skills = meta.GetValueOrDefault("skills", "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList(),
            CreatedAt = DateTime.TryParse(meta.GetValueOrDefault("createdAt", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow,
            Persona = body,
            IsDeveloper = bool.TryParse(meta.GetValueOrDefault("isDeveloper", "false"), out var isDev) && isDev,
            IsCeo = bool.TryParse(meta.GetValueOrDefault("isCeo", "false"), out var isCeo) && isCeo,
            ReportsToId = string.IsNullOrWhiteSpace(meta.GetValueOrDefault("reportsToId", "")) ? null : meta["reportsToId"],
            ReportsToName = string.IsNullOrWhiteSpace(meta.GetValueOrDefault("reportsToName", "")) ? null : meta["reportsToName"]
        };
    }
}
