using AgentOrchestrator.Core;

namespace AgentOrchestrator.Core.Tests;

public class PromptsTests
{
    [Fact]
    public void FormatPersonaPrompt_IncludesJobTitle()
    {
        var result = Prompts.FormatPersonaPrompt("Backend Developer", null, false);

        Assert.Contains("Backend Developer", result);
        Assert.Contains(Prompts.PersonaSuffix, result);
    }

    [Fact]
    public void FormatPersonaPrompt_IncludesPurpose_WhenProvided()
    {
        var result = Prompts.FormatPersonaPrompt("Designer", "Create UI mockups", false);

        Assert.Contains("Create UI mockups", result);
        Assert.Contains("Their purpose is", result);
    }

    [Fact]
    public void FormatPersonaPrompt_OmitsPurpose_WhenNull()
    {
        var result = Prompts.FormatPersonaPrompt("Engineer", null, false);

        Assert.DoesNotContain("Their purpose is", result);
    }

    [Fact]
    public void FormatPersonaPrompt_OmitsPurpose_WhenEmpty()
    {
        var result = Prompts.FormatPersonaPrompt("Engineer", "", false);

        Assert.DoesNotContain("Their purpose is", result);
    }

    [Fact]
    public void FormatPersonaPrompt_IncludesDeveloperContext_WhenDeveloper()
    {
        var result = Prompts.FormatPersonaPrompt("Developer", null, true);

        Assert.Contains(Prompts.DeveloperContext, result);
    }

    [Fact]
    public void FormatPersonaPrompt_OmitsDeveloperContext_WhenNotDeveloper()
    {
        var result = Prompts.FormatPersonaPrompt("Manager", null, false);

        Assert.DoesNotContain("hands-on developer", result);
    }

    [Fact]
    public void FormatSkillsPrompt_IncludesJobTitle()
    {
        var result = Prompts.FormatSkillsPrompt("Frontend Developer", null);

        Assert.Contains("Frontend Developer", result);
        Assert.Contains(Prompts.SkillsSuffix, result);
    }

    [Fact]
    public void FormatSkillsPrompt_IncludesPurpose_WhenProvided()
    {
        var result = Prompts.FormatSkillsPrompt("Developer", "Build APIs");

        Assert.Contains("Build APIs", result);
    }

    [Fact]
    public void FormatSkillsPrompt_OmitsPurpose_WhenNull()
    {
        var result = Prompts.FormatSkillsPrompt("Developer", null);

        Assert.DoesNotContain("Their purpose is", result);
    }

    [Fact]
    public void FormatTeamStructurePrompt_IncludesProjectDetails()
    {
        var result = Prompts.FormatTeamStructurePrompt("CloudSync", "A cloud storage platform");

        Assert.Contains("CloudSync", result);
        Assert.Contains("cloud storage platform", result);
    }

    [Fact]
    public void FormatTeamStructurePrompt_IncludesDeveloperDetectionInstructions()
    {
        var result = Prompts.FormatTeamStructurePrompt("Test", "Test");

        Assert.Contains(Prompts.TeamStructureDeveloperNote, result);
    }

    [Fact]
    public void FormatTeamStructurePrompt_IncludesJsonFormatExample()
    {
        var result = Prompts.FormatTeamStructurePrompt("Test", "Test");

        Assert.Contains("isDeveloper", result);
        Assert.Contains("reportsTo", result);
    }

    [Fact]
    public void FormatTeamStructurePrompt_IncludesReportsToNote()
    {
        var result = Prompts.FormatTeamStructurePrompt("Test", "Test");

        Assert.Contains(Prompts.TeamStructureReportsToNote, result);
    }
}
