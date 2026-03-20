namespace AgentOrchestrator.Core.Interfaces;

public interface IClaudeCodeRunner
{
    Task<string> ExecuteAsync(string prompt, string? workingDirectory = null);
}
