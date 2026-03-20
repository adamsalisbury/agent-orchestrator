# Architecture

## Solution Structure

The application follows a clean layered architecture with three projects:

```
src/
├── AgentOrchestrator.Core           # Domain layer
├── AgentOrchestrator.Infrastructure  # Data access and external integrations
└── AgentOrchestrator.Web            # Presentation layer
```

### AgentOrchestrator.Core

The domain layer with no external dependencies. Contains:

- **Models** — `Agent`, `Project`, `ThreadMessage`, `ClaudeRequest`
- **Interfaces** — `IAgentRepository`, `IProjectRepository`, `IThreadRepository`, `IClaudeCodeRunner`, `IClaudeRequestRepository`
- **Services** — `ClaudeOrchestrationService` (prompt construction and delegation handling), `ThreadOrchestrationService` (message queuing and processing)

### AgentOrchestrator.Infrastructure

Implements the interfaces defined in Core:

- **File-based repositories** — agents, threads, projects, and requests stored as JSON and Markdown files under `App_Data/`
- **ClaudeCodeCliRunner** — wraps the Claude Code CLI for executing agent prompts
- **AvatarGenerator** — creates deterministic SVG avatars from agent IDs

### AgentOrchestrator.Web

The ASP.NET Core MVC application:

- **Controllers** — `HomeController`, `MailboxController` (agents and messages), `ProjectController`
- **Views** — Razor views for all UI pages
- **Hubs** — `NotificationHub` for SignalR real-time updates
- **Services** — `PendingMessageTracker` (tracks in-flight requests), `RequestPollingService` (background service monitoring completion)

## Request Processing Flow

```
User sends message
    │
    ▼
MailboxController.Compose()
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
    │   ├── Team member list
    │   ├── Conversation history
    │   └── Consultation results (if applicable)
    │
    ▼
ClaudeCodeCliRunner.ExecuteAsync()
    │
    ▼
TryParseDelegation() — check response for delegation directive
    │
    ├── If delegation:
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
│   ├── workspace/          # Shared code workspace
│   └── shared/             # File exchange between agents
├── agent-{id}/
│   ├── persona.md          # Agent metadata and persona prompt
│   ├── avatar.svg          # Generated avatar
│   └── threads/
│       └── thread-{id}/
│           └── {threadId}-{n}.md  # Individual messages (Markdown with YAML frontmatter)
└── requests.json           # Request tracking
```

Messages are stored as Markdown files with YAML frontmatter containing metadata (thread ID, message number, direction, timestamp, status).

## Key Design Decisions

- **File-based storage** — no database setup required; data is human-readable and inspectable; suitable for single-user/local deployments
- **Background queue processing** — messages are queued via `System.Threading.Channels` and processed asynchronously, keeping the web UI responsive
- **Claude Code CLI integration** — agents execute via the CLI rather than a direct API, giving them access to tool use, file operations, and the full Claude Code capability set
- **Delegation depth limit** — capped at 5 levels to prevent runaway recursion between agents
- **No external NuGet packages** — the application runs entirely on built-in .NET 8.0 libraries
