using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Core.Services;
using NSubstitute;

namespace AgentOrchestrator.Core.Tests;

public class TeamServiceTests
{
    private readonly IClaudeCodeRunner _runner;
    private readonly IAgentRepository _agentRepo;
    private readonly AgentService _agentService;
    private readonly TeamService _service;

    public TeamServiceTests()
    {
        _runner = Substitute.For<IClaudeCodeRunner>();
        _agentRepo = Substitute.For<IAgentRepository>();
        _agentService = new AgentService(_runner);
        _service = new TeamService(_runner, _agentRepo, _agentService);
    }

    [Fact]
    public async Task GenerateTeamStructureAsync_ParsesValidJson()
    {
        var json = """
            [
              {"name": "Alex", "jobTitle": "CEO", "purpose": "Leads the company", "reportsTo": null, "isDeveloper": false},
              {"name": "Sarah", "jobTitle": "Lead Developer", "purpose": "Writes code", "reportsTo": "Alex", "isDeveloper": true}
            ]
            """;
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns(json);

        var result = await _service.GenerateTeamStructureAsync("Test Project", "A test project");

        Assert.Equal(2, result.Count);
        Assert.Equal("Alex", result[0].Name);
        Assert.Equal("CEO", result[0].JobTitle);
        Assert.Null(result[0].ReportsTo);
        Assert.False(result[0].IsDeveloper);
        Assert.Equal("Sarah", result[1].Name);
        Assert.Equal("Alex", result[1].ReportsTo);
        Assert.True(result[1].IsDeveloper);
    }

    [Fact]
    public async Task GenerateTeamStructureAsync_HandlesMarkdownFences()
    {
        var response = """
            Here is the team:
            ```json
            [{"name": "Alex", "jobTitle": "CEO", "purpose": "Leads", "reportsTo": null, "isDeveloper": false}]
            ```
            """;
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns(response);

        var result = await _service.GenerateTeamStructureAsync("Test", "Test");

        Assert.Single(result);
        Assert.Equal("Alex", result[0].Name);
    }

    [Fact]
    public async Task GenerateTeamStructureAsync_IncludesProjectDetailsInPrompt()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("""[{"name": "A", "jobTitle": "CEO", "purpose": "x", "reportsTo": null, "isDeveloper": false}]""");

        await _service.GenerateTeamStructureAsync("CloudSync", "A cloud storage platform");

        await _runner.Received(1).ExecuteAsync(
            Arg.Is<string>(p => p.Contains("CloudSync") && p.Contains("cloud storage platform")),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task CreateAgentFromRoleAsync_CreatesAgentWithGeneratedPersonaAndSkills()
    {
        _runner.ExecuteAsync(Arg.Is<string>(p => p.Contains("persona")), Arg.Any<string?>())
            .Returns("Generated persona text");
        _runner.ExecuteAsync(Arg.Is<string>(p => p.Contains("skill tags")), Arg.Any<string?>())
            .Returns("C#, .NET, SQL");

        var expectedAgent = new Agent { Id = "abc12", Name = "James", JobTitle = "Backend Developer" };
        _agentRepo.CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(expectedAgent);
        _agentRepo.GetAllAsync().Returns(new List<Agent>());

        var result = await _service.CreateAgentFromRoleAsync("James", "Backend Developer", "API work");

        Assert.Equal("James", result.Name);
        await _agentRepo.Received(1).CreateAsync(
            "James", "Backend Developer", "Generated persona text",
            Arg.Is<List<string>>(s => s.Count == 3),
            true, false, null, null);
    }

    [Fact]
    public async Task CreateAgentFromRoleAsync_ResolvesReportsToNameToId()
    {
        var manager = new Agent { Id = "mgr01", Name = "Sarah" };
        _agentRepo.GetAllAsync().Returns(new List<Agent> { manager });

        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns("output");
        _agentRepo.CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new Agent { Id = "dev01", Name = "James" });

        await _service.CreateAgentFromRoleAsync("James", "Developer", "Code", reportsToName: "Sarah");

        await _agentRepo.Received(1).CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(),
            "mgr01", "Sarah");
    }

    [Fact]
    public async Task CreateAgentFromRoleAsync_DetectsCeoFromJobTitle()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns("output");
        _agentRepo.GetAllAsync().Returns(new List<Agent>());
        _agentRepo.CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new Agent { Id = "ceo01", Name = "Alex" });

        await _service.CreateAgentFromRoleAsync("Alex", "Chief Executive Officer", "Lead");

        await _agentRepo.Received(1).CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            false, true, Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task CreateAgentFromRoleAsync_DetectsDeveloperFromJobTitle()
    {
        _runner.ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns("output");
        _agentRepo.GetAllAsync().Returns(new List<Agent>());
        _agentRepo.CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new Agent { Id = "dev01", Name = "James" });

        await _service.CreateAgentFromRoleAsync("James", "Senior Software Engineer", "Build things");

        await _agentRepo.Received(1).CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(),
            true, false, Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData("""[{"a":1}]""", """[{"a":1}]""")]
    [InlineData("""Some text [{"a":1}] more text""", """[{"a":1}]""")]
    [InlineData("""```json\n[{"a":1}]\n```""", """[{"a":1}]""")]
    [InlineData("no array here", "no array here")]
    public void ExtractJsonArray_ExtractsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, TeamService.ExtractJsonArray(input));
    }
}
