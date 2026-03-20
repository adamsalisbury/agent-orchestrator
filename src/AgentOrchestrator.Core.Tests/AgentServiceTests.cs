using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Services;
using NSubstitute;

namespace AgentOrchestrator.Core.Tests;

public class AgentServiceTests
{
    private readonly IClaudeCodeRunner _runner;
    private readonly AgentService _service;

    public AgentServiceTests()
    {
        _runner = Substitute.For<IClaudeCodeRunner>();
        _service = new AgentService(_runner);
    }

    [Fact]
    public async Task GeneratePersonaAsync_CallsRunnerWithFormattedPrompt()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("A skilled backend engineer focused on API design.");

        var result = await _service.GeneratePersonaAsync("Backend Developer", "Build REST APIs");

        Assert.Equal("A skilled backend engineer focused on API design.", result);
        await _runner.Received(1).ExecuteAsync(
            Arg.Is<string>(p => p.Contains("Backend Developer") && p.Contains("Build REST APIs")),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GeneratePersonaAsync_WithNoPurpose_OmitsPurposeContext()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("A versatile engineer.");

        await _service.GeneratePersonaAsync("Software Engineer");

        await _runner.Received(1).ExecuteAsync(
            Arg.Is<string>(p => !p.Contains("Their purpose is")),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GeneratePersonaAsync_WhenDeveloper_IncludesDeveloperContext()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("A hands-on coder.");

        await _service.GeneratePersonaAsync("Developer", isDeveloper: true);

        await _runner.Received(1).ExecuteAsync(
            Arg.Is<string>(p => p.Contains("hands-on developer who writes code")),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GeneratePersonaAsync_TrimsWhitespace()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("  persona with whitespace  \n");

        var result = await _service.GeneratePersonaAsync("Engineer");

        Assert.Equal("persona with whitespace", result);
    }

    [Fact]
    public async Task GenerateSkillsAsync_ReturnsCleanedSkillList()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("\"React\", 'TypeScript', CSS, API Design, Testing");

        var result = await _service.GenerateSkillsAsync("Frontend Developer");

        Assert.Equal(5, result.Count);
        Assert.Contains("React", result);
        Assert.Contains("TypeScript", result);
        Assert.Contains("CSS", result);
        Assert.Contains("API Design", result);
        Assert.Contains("Testing", result);
    }

    [Fact]
    public async Task GenerateSkillsAsync_CallsRunnerWithFormattedPrompt()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("C#, .NET, SQL");

        await _service.GenerateSkillsAsync("Backend Developer", "API work");

        await _runner.Received(1).ExecuteAsync(
            Arg.Is<string>(p => p.Contains("Backend Developer") && p.Contains("API work")),
            Arg.Any<string?>());
    }

    [Theory]
    [InlineData("React, Vue, Angular", 3)]
    [InlineData("Single", 1)]
    [InlineData("", 0)]
    [InlineData("  , ,  ,  ", 0)]
    [InlineData("\"Quoted\", 'Single Quoted', Plain", 3)]
    public void ParseSkillsCsv_HandlesVariousFormats(string input, int expectedCount)
    {
        var result = AgentService.ParseSkillsCsv(input);
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("Software Developer", true)]
    [InlineData("Backend Engineer", true)]
    [InlineData("Frontend Developer", true)]
    [InlineData("Full Stack Developer", true)]
    [InlineData("Full-Stack Engineer", true)]
    [InlineData("DevOps Engineer", true)]
    [InlineData("SRE Lead", true)]
    [InlineData("Product Manager", false)]
    [InlineData("CEO", false)]
    [InlineData("UX Designer", false)]
    [InlineData("Marketing Lead", false)]
    [InlineData("Project Manager", false)]
    public void IsDeveloperRole_DetectsCorrectly(string jobTitle, bool expected)
    {
        Assert.Equal(expected, AgentService.IsDeveloperRole(jobTitle));
    }

    [Theory]
    [InlineData("Chief Executive Officer", true)]
    [InlineData("CEO", true)]
    [InlineData("ceo", true)]
    [InlineData("VP of Engineering", false)]
    [InlineData("CTO", false)]
    [InlineData("Product Manager", false)]
    public void IsCeoRole_DetectsCorrectly(string jobTitle, bool expected)
    {
        Assert.Equal(expected, AgentService.IsCeoRole(jobTitle));
    }
}
