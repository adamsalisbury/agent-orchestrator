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

    public async Task<Agent> CreateAsync(string name, string jobTitle, string persona)
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
                CreatedAt = DateTime.UtcNow
            };

            var content = $"---\nagentId: {agent.Id}\nname: {agent.Name}\njobTitle: {agent.JobTitle}\ncreatedAt: {agent.CreatedAt:O}\n---\n\n{agent.Persona}";
            await File.WriteAllTextAsync(Path.Combine(agentDir, "persona.md"), content);

            var avatarSvg = AvatarGenerator.Generate(id);
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
            return ParseAgent(content);
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
                    agents.Add(agent);
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
            CreatedAt = DateTime.TryParse(meta.GetValueOrDefault("createdAt", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow,
            Persona = body
        };
    }
}
