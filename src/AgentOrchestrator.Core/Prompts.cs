namespace AgentOrchestrator.Core;

public static class Prompts
{
    public const string PersonaSuffix =
        "Cover their expertise, work approach, and responsibilities. " +
        "The agent will be part of a software development team collaborating with other specialist agents. " +
        "Keep it under 100 words. Output ONLY the persona text, no preamble or explanation.";

    public const string SkillsSuffix =
        "These are concise skill labels like \"UI/UX\", \"React\", \"API Design\", \"Code Review\". " +
        "Output ONLY a comma-separated list, no numbering, no explanation.";

    public const string DeveloperContext = " They are a hands-on developer who writes code.";

    public const string TeamStructureJsonFormat =
        """
        [
          {"name": "Alex", "jobTitle": "Chief Executive Officer", "purpose": "Oversees all operations, sets strategic direction, and ensures alignment across the team", "reportsTo": null, "isDeveloper": false},
          {"name": "Sarah", "jobTitle": "VP of Engineering", "purpose": "Leads the technical team and architecture decisions", "reportsTo": "Alex", "isDeveloper": false},
          {"name": "James", "jobTitle": "Senior Backend Developer", "purpose": "Implements server-side logic and API design", "reportsTo": "Sarah", "isDeveloper": true}
        ]
        """;

    public const string TeamStructureDeveloperNote =
        "IMPORTANT: For any role that involves writing code, programming, software development, " +
        "or engineering implementation, set isDeveloper to true. This includes roles like " +
        "\"Software Engineer\", \"Frontend Developer\", \"Backend Developer\", \"Full Stack Developer\", " +
        "\"DevOps Engineer\", etc. Management and design roles should be false.";

    public const string TeamStructureReportsToNote =
        "The reportsTo field should contain the name of the person they report to, or null for the CEO.";

    public static string FormatPersonaPrompt(string jobTitle, string? purpose, bool isDeveloper)
    {
        var purposeContext = string.IsNullOrWhiteSpace(purpose)
            ? ""
            : $" Their purpose is: \"{purpose}\".";

        var devContext = isDeveloper ? DeveloperContext : "";

        return $"Generate an agent persona/system prompt for an AI agent whose job title is: \"{jobTitle}\".{purposeContext}{devContext} {PersonaSuffix}";
    }

    public static string FormatSkillsPrompt(string jobTitle, string? purpose)
    {
        var purposeContext = string.IsNullOrWhiteSpace(purpose)
            ? ""
            : $" Their purpose is: \"{purpose}\".";

        return $"List 5-8 short skill tags for an AI agent whose job title is: \"{jobTitle}\".{purposeContext} {SkillsSuffix}";
    }

    public static string FormatTeamStructurePrompt(string projectName, string projectDescription)
    {
        return $"""
            You are designing an organisational structure for a project team. The project is:

            Project Name: "{projectName}"
            Project Description: "{projectDescription}"

            Design a team of between 5 and 10 people to work on this project. The team MUST include a CEO at the top, followed by the key roles needed for this specific type of project/product.

            The org chart should be realistic and tailored to the project — for example, a SaaS product needs different roles than a mobile game or a data platform.

            Each person needs a realistic first name, a job title, and a brief purpose statement describing their responsibilities.

            {TeamStructureDeveloperNote}

            Output ONLY valid JSON — no markdown fences, no explanation. Use this exact format:
            {TeamStructureJsonFormat}

            {TeamStructureReportsToNote}
            """;
    }
}
