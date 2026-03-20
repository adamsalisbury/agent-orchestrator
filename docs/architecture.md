# Architecture

## Solution Structure

The application follows a clean layered architecture with four projects:

```
src/
├── AgentOrchestrator.Core            # Domain layer
├── AgentOrchestrator.Core.Tests      # Unit tests
├── AgentOrchestrator.Infrastructure  # Data access and external integrations
└── AgentOrchestrator.Web             # Presentation layer (thin controllers)
```

### AgentOrchestrator.Core

The domain layer with no external dependencies. Contains:

- **Models** — `Agent`, `Project`, `ThreadMessage`, `ClaudeRequest`, `TeamRole`
- **Interfaces** — `IAgentRepository`, `IProjectRepository`, `IThreadRepository`, `IClaudeCodeRunner`
- **Services**:
  - `AgentService` — persona generation, skills generation, role detection (developer/CEO)
  - `TeamService` — org chart generation via Claude Code, agent-from-role creation with hierarchy resolution
  - `ThreadOrchestrationService` — message queuing, background processing, prompt construction, delegation handling
- **Prompts** — static class centralising all prompt templates as constants and format methods

### AgentOrchestrator.Core.Tests

Unit tests using xUnit and NSubstitute, covering all public methods in Core services:

- `AgentServiceTests` — persona/skills generation, CSV parsing, role detection
- `TeamServiceTests` — team structure parsing, JSON extraction, agent creation with hierarchy
- `ThreadOrchestrationServiceTests` — message sending, thread continuation, ID generation
- `PromptsTests` — all format methods, inclusion/exclusion of optional context

### AgentOrchestrator.Infrastructure

Implements the interfaces defined in Core:

- **File-based repositories** — agents, threads, and projects stored as JSON and Markdown files under `App_Data/`
- **ClaudeCodeCliRunner** — wraps the Claude Code CLI for executing agent prompts
- **AvatarGenerator** — creates deterministic SVG avatars from agent IDs with role badges (`</>` for developers, gold star for CEO)

### AgentOrchestrator.Web

The ASP.NET Core MVC presentation layer. Controllers are thin — they validate input, call Core services, and return views or JSON:

- **Controllers** — `HomeController`, `AgentsController` (agents, threads, messages, workspace browser), `ProjectController` (project settings, setup wizard)
- **Views** — Razor views for all UI pages
- **Hubs** — `NotificationHub` for SignalR real-time updates
- **Services** — `PendingMessageTracker` (tracks in-flight requests), `RequestPollingService` (background service monitoring completion)

## Request Processing Flow

```
User sends message
    │
    ▼
AgentsController.Compose()
    │
    ▼
ThreadOrchestrationService.SendMessageAsync()
    │
    ▼
Message queued via Channel<QueueItem>
    │
    ▼
ProcessQueueAsync() (background task)
    │
    ├── BuildPromptWithContext()
    │   ├── Agent persona
    │   ├── Project context
    │   ├── Organisational structure (manager, direct reports)
    │   ├── Communication rules
    │   ├── Conversation history
    │   └── Consultation results (if applicable)
    │
    ▼
ClaudeCodeCliRunner.ExecuteAsync()
    │   (developers run in their own workspace directory)
    │
    ▼
TryParseDelegation() — check response for delegation directive
    │
    ├── If delegation (to a connected agent only):
    │   ├── Create consultation thread for target agent
    │   ├── Queue consultation message
    │   └── Feed result back to original agent
    │
    └── If direct response:
        ├── Save response as Markdown
        └── Mark message as Completed
    │
    ▼
RequestPollingService detects completion
    │
    ▼
SignalR broadcasts to connected clients
    │
    ▼
UI updates in real time
```

## Data Storage

All data persists as files under `App_Data/`:

```
App_Data/
├── project/
│   ├── project.md          # Project name and description
│   ├── workspace/          # Shared project workspace
│   └── shared/             # File exchange between agents
├── agent-{id}/
│   ├── persona.md          # Agent metadata (including role flags, reporting line)
│   ├── avatar.svg          # Generated avatar with role badge
│   ├── workspace/          # Developer agents only — personal code workspace
│   └── threads/
│       └── thread-{id}/
│           └── {threadId}-{n}.md  # Individual messages (Markdown with YAML frontmatter)
└── requests.json           # Request tracking
```

Agent metadata in `persona.md` includes YAML frontmatter with: `agentId`, `name`, `jobTitle`, `skills`, `createdAt`, `isDeveloper`, `isCeo`, `reportsToId`, `reportsToName`.

## Key Design Decisions

- **File-based storage** — no database setup required; data is human-readable and inspectable; suitable for single-user/local deployments
- **Thin controllers** — all domain logic, prompt construction, and Claude Code interaction lives in Core services; controllers handle only HTTP concerns
- **Centralised prompts** — all prompt templates live in `Prompts.cs` as constants, making them easy to find, modify, and test
- **Org chart delegation** — agents can only delegate to their direct reports or manager, preventing arbitrary cross-team communication and enabling realistic task cascading
- **Developer workspaces** — developer agents execute Claude Code in their own workspace directory, keeping code output isolated and browsable
- **Background queue processing** — messages are queued via `System.Threading.Channels` and processed asynchronously, keeping the web UI responsive
- **Claude Code CLI integration** — agents execute via the CLI rather than a direct API, giving them access to tool use, file operations, and the full Claude Code capability set
- **Delegation depth limit** — capped at 5 levels to prevent runaway recursion between agents
