using System.Diagnostics;
using AgentOrchestrator.Core.Interfaces;

namespace AgentOrchestrator.Infrastructure.Services;

public class ClaudeCodeCliRunner : IClaudeCodeRunner
{
    private readonly string _workingDirectory;

    public ClaudeCodeCliRunner(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public async Task<string> ExecuteAsync(string prompt)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = $"--dangerously-skip-permissions -p \"{EscapeForShell(prompt)}\"",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                $"Claude Code exited with code {process.ExitCode}. Error: {error}");
        }

        return output;
    }

    private static string EscapeForShell(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
