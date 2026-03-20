# Setup Guide

## Prerequisites

### .NET 8.0 SDK

The application is built on .NET 8.0 (LTS). Install it from the official site:

- [Download .NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

Verify installation:

```bash
dotnet --version
# Should output 8.0.x
```

### Claude Code CLI

Agent Orchestrator uses Claude Code CLI to power agent responses. Install and authenticate it before running the application:

- [Claude Code documentation](https://docs.anthropic.com/en/docs/claude-code)

Verify the CLI is available and authenticated:

```bash
claude --version
```

## Building the Application

Clone the repository and build:

```bash
git clone <repository-url>
cd agent-orchestrator/src
dotnet build AgentOrchestrator.sln
```

## Running Tests

```bash
cd src
dotnet test
```

This runs the xUnit test suite covering all Core service logic.

## Running the Application

```bash
cd src
dotnet run --project AgentOrchestrator.Web
```

The application starts on **http://localhost:5181** by default.

### Development Mode

For hot-reload during development:

```bash
cd src
dotnet watch run --project AgentOrchestrator.Web
```

## Configuration

### Application Settings

Configuration is in `src/AgentOrchestrator.Web/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Data Storage

All data is stored in `src/AgentOrchestrator.Web/App_Data/`. This directory is created automatically and contains:

- Agent personas, avatars, and conversation threads
- Project configuration, shared workspace, and shared space
- Request tracking data

The `App_Data/` directory is excluded from version control via `.gitignore`.

### Port Configuration

The default port (5181) is set in `Program.cs`. To change it, modify the `UseUrls` call or use environment variables:

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run --project AgentOrchestrator.Web
```

## Verifying the Setup

1. Open `http://localhost:5181` in a browser
2. You'll be redirected to the **setup wizard** (since no project is configured yet)
3. Enter a company name, then click **Next: Project Details**
4. Enter a project name and description, then click **Next: Build Team**
5. Click **Generate Organisation** — this calls Claude Code to design an org chart and then generates each agent's persona and skills. If this completes, the Claude Code CLI integration is working.
6. Click **Finish Setup** to reach the Project page
7. Navigate to **Team** to see your agents with avatars, role badges, and reporting lines
