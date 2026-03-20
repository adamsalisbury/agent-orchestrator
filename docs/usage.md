# Usage Guide

## Setup Wizard

On first launch, you'll be redirected to the setup wizard. The navigation menu is hidden until setup is complete.

### Step 1: Company Name

1. Enter your **company or organisation name** (e.g., "Acme Corp")
2. Click **Next: Project Details**

### Step 2: Project Details

1. Enter a **project name** (e.g., "CloudSync Platform")
2. Enter a **project description** — describe what the team is building, the goals, target audience, and tech stack. The more detail you provide, the better the auto-generated team will match your needs.
3. Click **Next: Build Team**

### Step 3: Build Team

You have two options:

#### Auto-Generate Organisation

Click **Generate Organisation** to have an org chart designed automatically based on your project description. This creates 5-10 agents with:

- A **CEO** at the top
- Managers and specialists tailored to the project type
- Developers flagged with personal workspaces
- Reporting lines connecting every agent

Each agent gets a generated persona and skill set. Progress is shown as each agent is created.

#### Add Agents Manually

Use the inline form to add individual agents by name, job title, and purpose. Each one gets a generated persona and skills.

Once your team is ready, click **Finish Setup** to proceed to the Project page. The navigation menu (Project, Team, Communication) will now be visible in the header.

## Agent Profiles

Each agent has a detail page accessible by clicking **View** on the Team page.

The profile shows:

- **ID badge** — styled like an employee badge with the company name, avatar, name, job title, and joined date
- **Status** — Idle, Busy, or Blocked, with a description of what the agent is currently working on
- **General information** — name, job title, joined date, agent ID, role type
- **Persona** — the full generated persona text
- **Skills** — all tagged capabilities
- **Reporting structure** — who they report to and their direct reports

From the profile page you can access **Communication** (threads), **Workspace** (developers only), and **Send Message**.

## Sending Messages

1. From the **Team** page, click **View** on any agent to open their profile
2. Click **Communication** to see their threads, then **New Message**
3. Or click **Send Message** directly from the profile page

The agent processes the message asynchronously. For the CEO, try sending a high-level directive — they'll delegate tasks down the org chart to the appropriate team members.

## Live Task Status

While agents are working, the Team page and agent profiles show live status:

- **Busy** — the agent is actively processing a request, with a description of the task
- **Blocked** — the agent has delegated to another team member and is waiting for their response
- **Idle** — the agent has no pending work

Status is tracked via a `current-task.md` file that is created when processing starts and removed when the agent finishes.

## Viewing Conversations

### Agent Threads

- Navigate to **Team** from the top navigation
- Click **View** on an agent, then **Communication** to see their threads
- Each thread shows message count, preview, status, and last activity

### Thread View

- Click a thread to see the full conversation
- Messages are displayed chronologically with sender information
- Pending messages show a spinner while the agent is processing
- Use the reply form at the bottom to continue the conversation

### Communication

- Navigate to **Communication** from the top navigation for a global view across all agents
- Click any message to view it in full detail with sender/recipient avatars

## Understanding Delegation

Delegation follows the organisational hierarchy:

1. An agent determines a task should be handled by a direct report or escalated to their manager
2. The agent emits a delegation directive (handled automatically)
3. A **consultation thread** is created for the target agent
4. The target agent processes the task — if they're a developer, they work in their own workspace
5. The result is fed back to the original agent

Delegation is **restricted to the reporting line** — an agent can only communicate with their direct manager and direct reports. This enables realistic chains: CEO delegates to VP, VP delegates to Developer, results cascade back up.

Delegation is limited to 5 levels deep to prevent infinite loops.

## Developer Workspaces

Developer agents write code in personal workspace directories.

### Developer Peer Collaboration

Developers are aware of each other and are instructed to use the shared directory (`../shared/`) to coordinate. For example, a frontend developer and backend developer can agree on API contracts by writing shared specification files that both reference.

### Browsing a Workspace

1. Navigate to **Team**, click **View** on a developer agent
2. Click the **`</>` Workspace** button
3. The workspace browser shows:
   - **Left panel (25%)** — directory and file navigator with `..` to go up (capped at the workspace root)
   - **Right panel (75%)** — plain text file viewer for the selected file

### Accessing Workspaces Externally

Developer workspaces are stored at `App_Data/agent-{id}/workspace/`. You can open these directories directly in VS Code or any editor — either locally or via SSH into the container running the application.

## Org Chart & Role Badges

- **CEO** agents display a gold star badge on their avatar and a "CEO" label in the UI
- **Developer** agents display a `</>` badge on their avatar and a code label in the UI
- Each agent card shows a live status badge (Idle / Busy / Blocked)
- Agent profile pages display an ID badge and full reporting structure
- The **Project** page shows the full team roster

## Real-Time Updates

The application uses SignalR to push updates when:

- An agent finishes processing a message
- Message status changes (Pending → Processing → Completed/Failed)

Pages automatically refresh relevant content when updates arrive — no manual reload needed.

## Dark Mode

Click the theme toggle button (sun/moon icon) in the navigation bar to switch between light and dark themes. The preference is saved in your browser.

## Resetting the Environment

To start fresh — removing all agents, threads, and project data:

1. Click the **Reset** button in the navigation bar
2. Confirm the action in the modal dialog

This deletes all data in `App_Data/` and is irreversible. You'll be redirected to the setup wizard.
