# Application Overview

## What Is Agent Orchestrator?

Agent Orchestrator is a web-based platform for creating and managing teams of AI agents that collaborate to complete tasks. It provides a centralised interface for defining agents with distinct personas and skills, organising them into a reporting hierarchy, sending them work requests, and watching them coordinate through delegation along the chain of command.

Rather than interacting with a single AI assistant, Agent Orchestrator models a **software development company** — the user is the client, the CEO takes their brief, managers coordinate, developers write code in a shared workspace, and DevOps engineers host the finished product.

## Core Concepts

### Agents

An agent is an AI team member with:

- **Name and job title** — defines the agent's role (e.g., "Sarah — VP of Engineering")
- **Persona** — a generated system prompt describing their expertise, communication style, and responsibilities
- **Skills** — a set of tagged capabilities (e.g., "API Design", "Architecture", "Code Review")
- **Avatar** — a procedurally generated SVG face, unique to each agent, with role badges (`</>` for developers, gold star for the CEO)
- **Reporting line** — who they report to and who reports to them
- **Profile page** — a detail view with an ID badge, general information, live task status, and access to communication and workspaces

### Developer & DevOps Agents

Agents whose role involves writing code or infrastructure are flagged as developers. They receive:

- Access to the **shared project workspace** where all developers write code together
- A `</>` badge on their avatar and in the UI
- **Peer awareness** — developers and DevOps engineers know about each other and coordinate via the shared space (e.g., agreeing on API contracts)

**DevOps engineers** have additional responsibilities: once developers complete their work, DevOps reviews the workspace, runs the project (e.g., `dotnet run`, `npx http-server`), and reports the URL where the hosted application can be accessed.

### Organisational Hierarchy

Agents are arranged in a reporting structure:

- The **CEO** sits at the top and can delegate to their direct reports
- Each agent knows their manager and their direct reports
- **Delegation is restricted** to the reporting line — agents can only communicate with their direct manager and direct reports
- This enables realistic task cascading: the CEO delegates to a VP, who delegates to a developer

### Company & Project

The platform is organised around a company and its project:

- **Company name** — the name of the organisation, displayed in the header throughout the application
- **Project name and description** — what the team is building, shared as context with all agents
- **Workspace** — the shared code directory where all developers and DevOps engineers work, browsable from the Project page
- **Shared space** — for exchanging specifications, API contracts, and coordination files between agents, also browsable from the Project page
- **Team roster** — all agents can see who else is on the team and their roles

### Threads and Messages

Communication happens through threaded conversations:

- Each request to an agent starts a new thread
- Messages within a thread maintain full conversation history
- Agents receive prior context when replying, enabling multi-turn conversations
- Messages track status: Pending, Processing, Completed, or Failed

### Delegation

Agents delegate tasks through the org chart:

- When an agent determines a direct report or manager is better suited, it emits a delegation directive
- The system routes the task to the target agent in a consultation thread
- The consultation result is fed back to the original agent
- Delegation supports up to 5 levels of depth to prevent infinite loops
- Delegation is **only permitted between connected agents** in the reporting hierarchy

## How It Differs from a Simple Chat Interface

| Feature | Simple Chat | Agent Orchestrator |
|---------|------------|-------------------|
| Number of agents | 1 | Many, each with distinct roles |
| Agent identity | Generic | Custom persona, skills, avatar |
| Organisation | None | CEO-led hierarchy with reporting lines |
| Collaboration | None | Agents delegate along chain of command |
| Code output | Inline in chat | Written to shared workspace, browsable via Project page |
| Hosting | Manual | DevOps agents run and host the project, CEO reports URL |
| Project awareness | Per-conversation | Persistent project context shared across all agents |
| Conversation tracking | Linear | Threaded with full history |
| Real-time updates | Request/response | SignalR live notifications |
