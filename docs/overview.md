# Application Overview

## What Is Agent Orchestrator?

Agent Orchestrator is a web-based platform for creating and managing teams of AI agents that collaborate to complete tasks. It provides a centralised interface for defining agents with distinct personas and skills, organising them into a reporting hierarchy, sending them work requests, and watching them coordinate through delegation along the chain of command.

Rather than interacting with a single AI assistant, Agent Orchestrator models a **team dynamic** — a CEO oversees the organisation, managers coordinate their reports, and developers write code in their own workspaces.

## Core Concepts

### Agents

An agent is an AI team member with:

- **Name and job title** — defines the agent's role (e.g., "Sarah — VP of Engineering")
- **Persona** — a generated system prompt describing their expertise, communication style, and responsibilities
- **Skills** — a set of tagged capabilities (e.g., "API Design", "Architecture", "Code Review")
- **Avatar** — a procedurally generated SVG face, unique to each agent, with role badges (`</>` for developers, gold star for the CEO)
- **Reporting line** — who they report to and who reports to them

### Developer Agents

Agents whose role involves writing code are flagged as developers. They receive:

- A **personal workspace directory** where they write code when given implementation tasks
- A `</>` badge on their avatar and in the UI
- A **workspace browser** accessible from the Agents page, allowing you to browse files and view code they've written

### Organisational Hierarchy

Agents are arranged in a reporting structure:

- The **CEO** sits at the top and can delegate to their direct reports
- Each agent knows their manager and their direct reports
- **Delegation is restricted** to the reporting line — agents can only communicate with their direct manager and direct reports
- This enables realistic task cascading: the CEO delegates to a VP, who delegates to a developer

### Projects

A project provides shared context that all agents understand. It includes:

- **Name and description** — what the team is working on
- **Shared directory** — for exchanging files between agents (specs, notes, documentation)
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
| Code output | Inline in chat | Written to developer workspaces, browsable via UI |
| Project awareness | Per-conversation | Persistent project context shared across all agents |
| Conversation tracking | Linear | Threaded with full history |
| Real-time updates | Request/response | SignalR live notifications |
