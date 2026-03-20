using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Infrastructure.Data;

public class FileProjectRepository : IProjectRepository
{
    private readonly string _projectDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileProjectRepository(string baseDirectory)
    {
        _projectDir = Path.Combine(baseDirectory, "project");
        Directory.CreateDirectory(_projectDir);
        Directory.CreateDirectory(Path.Combine(_projectDir, "shared"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "workspace"));
    }

    public async Task<Project?> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_projectDir, "project.md");
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllTextAsync(filePath);
            return ParseProject(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(Project project)
    {
        await _lock.WaitAsync();
        try
        {
            var content = $"---\ncompanyName: {project.CompanyName}\nname: {project.Name}\n---\n\n{project.Description}";
            await File.WriteAllTextAsync(Path.Combine(_projectDir, "project.md"), content);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task DeleteAsync()
    {
        var filePath = Path.Combine(_projectDir, "project.md");
        if (File.Exists(filePath))
            File.Delete(filePath);

        // Clear shared and workspace directories
        var sharedPath = GetSharedPath();
        if (Directory.Exists(sharedPath))
            Directory.Delete(sharedPath, recursive: true);
        Directory.CreateDirectory(sharedPath);

        var workspacePath = GetWorkspacePath();
        if (Directory.Exists(workspacePath))
            Directory.Delete(workspacePath, recursive: true);
        Directory.CreateDirectory(workspacePath);

        return Task.CompletedTask;
    }

    public string GetWorkspacePath() => Path.Combine(_projectDir, "workspace");

    public string GetSharedPath() => Path.Combine(_projectDir, "shared");

    private static Project? ParseProject(string fileContent)
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

        return new Project
        {
            CompanyName = meta.GetValueOrDefault("companyName", ""),
            Name = meta.GetValueOrDefault("name", ""),
            Description = body
        };
    }
}
