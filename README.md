# Agent Orchestrator

A multi-agent orchestration platform that enables creating, managing, and coordinating teams of AI agents. Agents are organised into a reporting hierarchy with a CEO at the top, can receive tasks, hold threaded conversations, and delegate work up and down the chain of command.

## What It Does

- **Setup wizard** — 3-step wizard: company name, project details, team generation
- **Auto-generated organisations** — describe your project and an entire org chart (CEO + 5-10 tailored roles) is created automatically, each with a generated persona and skill set
- **Organisational hierarchy** — agents are connected via a reporting structure; delegation is restricted to direct reports and managers
- **Developer workspaces** — agents flagged as developers get personal workspace directories where they write code, browsable via a built-in file viewer
- **Agent-to-agent delegation** — agents delegate tasks through the org chart, up to 5 levels deep
- **Threaded conversations** — send messages to agents and track multi-turn conversations
- **Real-time updates** — SignalR pushes live status changes as agents process requests
- **Agent profiles** — each agent has a detail page with an ID badge, persona, skills, reporting structure, and live task status
- **Live task status** — agents report what they're currently working on and who they're blocked by, visible on cards and profile pages
- **Developer peer collaboration** — developers are aware of each other and use the shared directory to coordinate (e.g., agreeing on API contracts)
- **Role badges** — developer agents display a `</>` badge, the CEO gets a gold star, both on avatars and in the UI
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
| **Testing** | xUnit, NSubstitute |
| **Avatars** | Procedurally generated SVGs with role badges |

## Project Structure

```
agent-orchestrator/
├── src/
│   ├── AgentOrchestrator.sln
│   ├── AgentOrchestrator.Core/            # Domain models, services, prompts, interfaces
│   ├── AgentOrchestrator.Core.Tests/      # Unit tests (xUnit + NSubstitute)
│   ├── AgentOrchestrator.Infrastructure/  # File repositories, Claude CLI runner, avatar generation
│   └── AgentOrchestrator.Web/             # MVC controllers (thin), views, SignalR hub
└── docs/                                  # Documentation
```

## Dependencies

### Runtime

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) — must be installed and authenticated

### NuGet Packages

- **NSubstitute** (test project only) — mocking framework for unit tests

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

### Running Tests

```bash
cd src
dotnet test
```

### First Steps

1. Navigate to the web UI — you'll be redirected to the setup wizard
2. Enter a company name, then project name and description
3. Click **Generate Organisation** to auto-create a team with a CEO and tailored roles, or add agents manually
4. Click **Finish Setup** — you'll land on the Project page
5. Navigate to **Team** to view your agents, then send the CEO a directive and watch tasks cascade down the org chart

## Architecture

The solution follows a clean layered architecture:

- **Core** — domain models (`Agent`, `Project`, `ThreadMessage`, `TeamRole`), service interfaces, domain services (`AgentService`, `TeamService`, `ThreadOrchestrationService`), and centralised prompt templates (`Prompts`)
- **Infrastructure** — file-based repositories persisting data as JSON/Markdown under `App_Data/`, the Claude Code CLI runner, and avatar generation
- **Web** — thin ASP.NET Core MVC controllers (presentation only), Razor views, SignalR hub for real-time notifications, and a background polling service

Controllers delegate immediately to Core services — all domain logic, prompt construction, and Claude Code interaction lives in the Core layer.

All data is stored on disk — no database setup required. Agent conversations, personas, and project configuration are persisted as structured files.

## Licence

All rights reserved.
