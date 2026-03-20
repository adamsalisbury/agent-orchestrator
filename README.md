# Agent Orchestrator

A multi-agent orchestration platform that enables creating, managing, and coordinating teams of AI agents. Agents can receive tasks, hold threaded conversations, and delegate work to each other based on their specialised skills and personas.

## What It Does

- **Create AI agents** with custom personas, job titles, and skill sets — personas are generated via Claude Code CLI
- **Send requests** to agents and track threaded conversations
- **Agent-to-agent delegation** — agents can consult teammates for specialised expertise, up to 5 levels deep
- **Project context** — define a project that all agents understand and work within, with shared workspace directories
- **Real-time updates** — SignalR pushes live status changes as agents process requests
- **Dark/light mode** — theme toggle with persistent preference

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Runtime** | .NET 8.0 (LTS) |
| **Web Framework** | ASP.NET Core MVC |
| **Real-time** | SignalR (WebSockets) |
| **AI Backend** | Claude Code CLI |
| **Data Storage** | File-based (JSON + Markdown in `App_Data/`) |
| **Frontend** | Bootstrap 5, jQuery, Razor Views |
| **Avatars** | Procedurally generated SVGs |

## Project Structure

```
agent-orchestrator/
├── src/
│   ├── AgentOrchestrator.sln
│   ├── AgentOrchestrator.Core/          # Domain models, interfaces, services
│   ├── AgentOrchestrator.Infrastructure/ # File repositories, Claude CLI runner, avatar generation
│   └── AgentOrchestrator.Web/           # MVC controllers, views, SignalR hub, background services
└── docs/                                # Documentation
```

## Dependencies

### Runtime

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) — must be installed and authenticated

### NuGet Packages

The project uses only built-in .NET 8.0 libraries — no external NuGet packages are required.

### Frontend Libraries (bundled in `wwwroot/lib/`)

- Bootstrap 5
- jQuery
- jQuery Validation
- Microsoft SignalR JavaScript client

## Getting Started

### Prerequisites

1. Install the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Install and authenticate [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code)

### Build & Run

```bash
cd src
dotnet build AgentOrchestrator.sln
dotnet run --project AgentOrchestrator.Web
```

The application starts on `http://localhost:5181`.

### First Steps

1. Navigate to the web UI
2. Create a project with a name and description
3. Create one or more agents — provide a name and job title, and the system will generate a persona and skills
4. Send a request to an agent and watch the threaded conversation unfold
5. Agents will delegate to teammates when a task requires specialised expertise

## Architecture

The solution follows a clean layered architecture:

- **Core** — domain models (`Agent`, `Project`, `ThreadMessage`, `ClaudeRequest`), service interfaces, and orchestration logic
- **Infrastructure** — file-based repositories persisting data as JSON/Markdown under `App_Data/`, the Claude Code CLI runner, and avatar generation
- **Web** — ASP.NET Core MVC application with Razor views, a SignalR hub for real-time notifications, and a background polling service that monitors request completion

All data is stored on disk — no database setup required. Agent conversations, personas, and project configuration are persisted as structured files.

## Licence

All rights reserved.
