# Application Overview

## What Is Agent Orchestrator?

Agent Orchestrator is a web-based platform for creating and managing teams of AI agents that collaborate to complete tasks. It provides a centralised interface for defining agents with distinct personas and skills, sending them work requests, and watching them coordinate through delegation and consultation.

Rather than interacting with a single AI assistant, Agent Orchestrator models a **team dynamic** — each agent has a defined role, expertise, and personality, and agents can ask each other for help when a task falls outside their specialisation.

## Core Concepts

### Agents

An agent is an AI team member with:

- **Name and job title** — defines the agent's role (e.g., "Sarah — Senior Backend Engineer")
- **Persona** — a generated system prompt describing their expertise, communication style, and responsibilities
- **Skills** — a set of tagged capabilities (e.g., "API Design", "Database Optimisation", "Code Review")
- **Avatar** — a procedurally generated SVG face, unique to each agent

### Projects

A project provides shared context that all agents understand. It includes:

- **Name and description** — what the team is working on
- **Workspace directory** — a shared filesystem path where agents can read and write code
- **Shared directory** — for exchanging files between agents
- **Team roster** — all agents can see who else is on the team and what they do

### Threads and Messages

Communication happens through threaded conversations:

- Each request to an agent starts a new thread
- Messages within a thread maintain full conversation history
- Agents receive prior context when replying, enabling multi-turn conversations
- Messages track status: Pending, Processing, Completed, or Failed

### Delegation

Agents can delegate tasks to teammates:

- When an agent determines another team member is better suited for part of a task, it emits a delegation directive
- The system automatically routes the question to the target agent in a consultation thread
- The consultation result is fed back to the original agent, which incorporates the answer into its response
- Delegation supports up to 5 levels of depth to prevent infinite loops

## How It Differs from a Simple Chat Interface

| Feature | Simple Chat | Agent Orchestrator |
|---------|------------|-------------------|
| Number of agents | 1 | Many, each with distinct roles |
| Agent identity | Generic | Custom persona, skills, avatar |
| Collaboration | None | Agents delegate and consult |
| Project awareness | Per-conversation | Persistent project context |
| Conversation tracking | Linear | Threaded with full history |
| Real-time updates | Request/response | SignalR live notifications |
