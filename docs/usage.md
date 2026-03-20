# Usage Guide

## Creating a Project

Before creating agents, set up a project to give them shared context.

1. Navigate to **Projects** from the top navigation
2. Click **Edit** to set the project name and description
3. Describe what the team is working on — all agents will receive this context with every request

The project also provides:

- A **workspace** directory where agents can read and write code
- A **shared** directory for exchanging files between agents

## Creating Agents

1. Navigate to **Agents** from the top navigation
2. Click **Create New Agent**
3. Fill in:
   - **Name** — the agent's display name
   - **Job Title** — their role (e.g., "Senior Backend Engineer", "UX Designer")
   - **Purpose** (optional) — additional context for persona generation
4. Click **Generate** — the system uses Claude Code to create a persona and skill set
5. Review and edit the generated persona and skills
6. Click **Save** to create the agent

Each agent gets a unique procedurally generated avatar.

## Sending Requests

1. Navigate to **New Request** from the top navigation (or click the button on the home page)
2. Select the target agent from the dropdown — a preview of their avatar appears
3. Write your message
4. Click **Send**

This creates a new thread. The agent processes the request asynchronously.

## Viewing Conversations

### Agent Inbox

- Navigate to **Agents** to see all agents with their request counts
- Click an agent's name to see their threads
- Each thread shows message count, preview, status, and last activity

### Thread View

- Click a thread to see the full conversation
- Messages are displayed chronologically with sender information
- Pending messages show a spinner while the agent is processing
- Use the reply form at the bottom to continue the conversation

### All Messages

- Navigate to **All Messages** for a global view across all agents
- Messages can be filtered and searched
- Click any message to view it in detail

## Understanding Delegation

When an agent determines that another team member is better suited for part of a task:

1. The agent emits a delegation directive (handled automatically)
2. A **consultation thread** is created in the target agent's inbox
3. The target agent processes the question
4. The answer is fed back to the original agent
5. The original agent incorporates the consultation into its response

Delegation indicators appear as badges on messages. You can follow the consultation chain by viewing the target agent's threads.

Delegation is limited to 5 levels deep to prevent infinite loops.

## Real-Time Updates

The application uses SignalR to push updates when:

- An agent finishes processing a request
- Message status changes (Pending → Processing → Completed/Failed)

Pages automatically refresh relevant content when updates arrive — no manual reload needed.

## Dark Mode

Click the theme toggle button (sun/moon icon) in the navigation bar to switch between light and dark themes. The preference is saved in your browser.

## Resetting the Environment

To start fresh — removing all agents, threads, and project data:

1. Click the **Reset** button in the navigation bar
2. Confirm the action in the modal dialog

This deletes all data in `App_Data/` and is irreversible.
