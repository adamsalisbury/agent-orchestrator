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
- Project configuration
- Request tracking data

The `App_Data/` directory is excluded from version control via `.gitignore`.

### Port Configuration

The default port (5181) is set in `Program.cs`. To change it, modify the `UseUrls` call or use environment variables:

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run --project AgentOrchestrator.Web
```

## Verifying the Setup

1. Open `http://localhost:5181` in a browser
2. You should see the landing page with options to create agents and send requests
3. Navigate to **Projects** and create a project to confirm the data layer is working
4. Create an agent — the persona generation step confirms Claude Code CLI integration is functioning
