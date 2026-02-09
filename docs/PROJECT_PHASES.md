# DraftStream - Project Build Phases

## Overview

DraftStream is a .NET 8+ AI agent that captures notes, tasks, and code snippets from Telegram and stores them in Notion databases using OpenRouter for LLM processing and Notion MCP server for database interaction.

## Architecture Decisions

| Decision | Choice |
|----------|--------|
| Architecture | Clean Architecture |
| Workflow separation | Separate assembly per workflow |
| Secrets management | Infisical (self-hosted) |
| Logging | Serilog → Seq |
| Observability | OpenTelemetry (distributed tracing) |
| Telegram mode | Long Polling (webhook as future enhancement) |
| Deployment | Docker container (future: VM/VPS) |
| Testing | Skipped for MVP |

## Solution Structure

```
DraftStream/
├── src/
│   ├── DraftStream.Domain/                 # Entities, Value Objects, Enums
│   ├── DraftStream.Application/            # Shared interfaces, abstractions, DTOs
│   ├── DraftStream.Application.Notes/      # Notes use cases & prompt
│   ├── DraftStream.Application.Tasks/      # Tasks use cases & prompt
│   ├── DraftStream.Application.Snippets/   # Snippets use cases & prompt
│   ├── DraftStream.Infrastructure/         # Telegram, OpenRouter, MCP, Infisical, Observability
│   └── DraftStream.Host/                   # Composition root, DI, Program.cs
├── docker-compose.yml
├── docker-compose.prod.yml
├── Dockerfile
├── Directory.Build.props
├── .editorconfig
└── DraftStream.sln
```

---

## Phase Summary

| Phase | Name | Goal |
|-------|------|------|
| 0 | Foundation | Solution scaffolding, tooling, Docker, observability setup |
| 1 | Telegram | Bot integration with topic routing |
| 2 | OpenRouter | LLM client with tool calling support |
| 3 | Notion MCP | MCP client for Notion operations |
| 4 | Notes | First end-to-end workflow |
| 5 | Tasks | Workflow with rich schema (priority, dates) |
| 6 | Snippets | Code/command focused workflow |
| 7 | Hardening | Health checks, error handling, production readiness |
| 8 | Future | Webhooks, read/search, multi-user |

---

## Phase 0 — Foundation & Solution Scaffolding

**Goal**: Set up the solution structure with Clean Architecture, configure tooling and infrastructure.

### Tasks

- [ ] Create solution file `DraftStream.sln`
- [ ] Create `Directory.Build.props` with shared settings (C# 12, nullable, implicit usings)
- [ ] Create `.editorconfig` for code style
- [ ] Create project shells:
  - [ ] `DraftStream.Domain` (classlib)
  - [ ] `DraftStream.Application` (classlib)
  - [ ] `DraftStream.Application.Notes` (classlib)
  - [ ] `DraftStream.Application.Tasks` (classlib)
  - [ ] `DraftStream.Application.Snippets` (classlib)
  - [ ] `DraftStream.Infrastructure` (classlib)
  - [ ] `DraftStream.Host` (console/worker)
- [ ] Set up project references (dependency flow: Host → Infrastructure → Application → Domain)
- [ ] Configure Infisical SDK for secrets management
- [ ] Configure Serilog with Seq sink
- [ ] Configure OpenTelemetry (ActivitySource, traces to console initially)
- [ ] Create `Dockerfile` (multi-stage build)
- [ ] Create `docker-compose.yml` with:
  - [ ] App service
  - [ ] Seq container (datalust/seq)
- [ ] Create `.gitignore` updates for .NET + secrets

---

## Phase 1 — Telegram Bot Integration

**Goal**: Receive messages from Telegram, identify topic/panel, log everything.

### Tasks

- [ ] Define `ITelegramBotService` interface in Application
- [ ] Create configuration models:
  - [ ] `TelegramSettings` (BotToken, GroupId, TopicMappings)
  - [ ] `TopicMapping` (TopicId → WorkflowType enum)
- [ ] Implement `TelegramBotService` in Infrastructure:
  - [ ] Long Polling receiver
  - [ ] Message handler
  - [ ] Topic identification from `message.MessageThreadId`
- [ ] Implement `TopicRouter`:
  - [ ] Map topic ID → workflow type (Notes/Tasks/Snippets)
  - [ ] Handle unknown topics gracefully
- [ ] Register as hosted service in Host
- [ ] Implement graceful shutdown (CancellationToken handling)
- [ ] Add tracing spans for message receipt
- [ ] Verify: Bot receives messages, logs show topic routing

---

## Phase 2 — OpenRouter LLM Client

**Goal**: Send prompts to OpenRouter, receive structured responses with tool calls.

### Tasks

- [ ] Define `ILlmClient` interface in Application
- [ ] Create models:
  - [ ] `LlmRequest` (messages, tools, model override)
  - [ ] `LlmResponse` (content, tool calls)
  - [ ] `LlmMessage`, `LlmToolCall`, `LlmToolDefinition`
- [ ] Implement `OpenRouterClient` in Infrastructure:
  - [ ] HttpClient with base URL `https://openrouter.ai/api/v1`
  - [ ] OpenAI-compatible chat completion endpoint
  - [ ] Tool/function calling support
- [ ] Configuration:
  - [ ] `OpenRouterSettings` (ApiKey, DefaultModel, ModelOverrides per workflow)
- [ ] Add Polly retry policy:
  - [ ] Retry on 429 (rate limit) with exponential backoff
  - [ ] Retry on transient HTTP errors
- [ ] Add tracing spans for LLM calls (model, tokens, duration)
- [ ] Verify: Can send prompt and receive structured response

---

## Phase 3 — Notion MCP Client

**Goal**: Connect to Notion MCP server, expose tools for page creation.

### Tasks

- [ ] Define `INotionMcpClient` interface in Application
- [ ] Implement `NotionMcpClient` in Infrastructure:
  - [ ] Use ModelContextProtocol SDK
  - [ ] Spawn `npx @notionhq/notion-mcp-server` as stdio process
  - [ ] Initialize connection, discover tools
  - [ ] Tool invocation with JSON arguments
- [ ] Configuration:
  - [ ] `NotionSettings` (IntegrationToken, DatabaseIds for each workflow)
- [ ] Implement process lifecycle management:
  - [ ] Start on first use
  - [ ] Health check / restart on failure
  - [ ] Clean shutdown
- [ ] Add tracing spans for MCP tool calls
- [ ] Verify: Can list tools, create a test page in Notion

---

## Phase 4 — Notes Workflow (End-to-End)

**Goal**: First complete workflow — message in Notes topic → saved in Notion.

### Tasks

- [ ] **Domain** (`DraftStream.Domain`):
  - [ ] `Note` entity (Title, Content, Tags, Source, CreatedAt)
  - [ ] `Tag` value object
  - [ ] `WorkflowType` enum (Notes, Tasks, Snippets)
- [ ] **Application.Notes** (`DraftStream.Application.Notes`):
  - [ ] `ProcessNoteCommand` (message text, metadata)
  - [ ] `ProcessNoteHandler` (MediatR handler)
  - [ ] `NoteDto` (extracted: title, content, tags[])
  - [ ] `notes-prompt.md` — system prompt for LLM extraction
  - [ ] Tool definition for Notion page creation
- [ ] **Infrastructure**:
  - [ ] `NotesWorkflowService` — orchestrates LLM → MCP flow
  - [ ] Wire up Telegram message → MediatR command
- [ ] **Host**:
  - [ ] Register Notes workflow in DI
- [ ] Implement confirmation message back to Telegram
- [ ] Verify: Send message in Notes topic → appears in Notion → confirmation in Telegram
- [ ] Verify: Full trace visible in Seq

---

## Phase 5 — Tasks Workflow

**Goal**: Second workflow with richer schema (priority, due date, project).

### Tasks

- [ ] **Domain**:
  - [ ] `TaskItem` entity (Title, Description, Status, Priority, Project, Labels, DueDate)
  - [ ] `Priority` enum (Low, Medium, High, Urgent)
  - [ ] `TaskStatus` enum (NotStarted, InProgress, Done)
  - [ ] `Project` value object
- [ ] **Application.Tasks** (`DraftStream.Application.Tasks`):
  - [ ] `ProcessTaskCommand` / `ProcessTaskHandler`
  - [ ] `TaskDto` (extracted fields)
  - [ ] `tasks-prompt.md` — system prompt with date parsing hints
  - [ ] Tool definition for task creation
- [ ] Implement relative date parsing in prompt:
  - [ ] "friday" → next Friday's date
  - [ ] "next week" → Monday of next week
  - [ ] "tomorrow", "in 3 days", etc.
- [ ] Format confirmation message with task details
- [ ] Verify: Task message → Notion with correct properties → formatted confirmation

---

## Phase 6 — Snippets Workflow

**Goal**: Third workflow optimized for code/commands.

### Tasks

- [ ] **Domain**:
  - [ ] `Snippet` entity (Title, Code, Language, Tags, Description)
  - [ ] `Language` enum (Bash, Sql, CSharp, Docker, Git, Python, JavaScript, Other)
- [ ] **Application.Snippets** (`DraftStream.Application.Snippets`):
  - [ ] `ProcessSnippetCommand` / `ProcessSnippetHandler`
  - [ ] `SnippetDto` (extracted fields)
  - [ ] `snippets-prompt.md` — system prompt with:
    - [ ] Code block detection
    - [ ] Language auto-detection hints
    - [ ] Preserve formatting
- [ ] Handle Telegram code formatting (``` blocks)
- [ ] Verify: Code/command message → Notion snippet → confirmation

---

## Phase 7 — Polish & Hardening

**Goal**: Production-ready reliability and observability.

### Tasks

- [ ] **Health Checks**:
  - [ ] `/health` endpoint (liveness)
  - [ ] `/ready` endpoint (readiness — checks Telegram, MCP connection)
  - [ ] Health check for Notion MCP process
- [ ] **Error Handling**:
  - [ ] User-friendly error messages to Telegram
  - [ ] Structured error logging
  - [ ] Don't leak internal errors to user
- [ ] **Resilience**:
  - [ ] Rate limit awareness (OpenRouter, Telegram)
  - [ ] Circuit breaker for external services
  - [ ] Retry queue for failed operations (optional)
- [ ] **Docker Optimization**:
  - [ ] Multi-stage build (restore → build → publish → runtime)
  - [ ] Non-root user
  - [ ] `.dockerignore`
- [ ] **Production Compose**:
  - [ ] `docker-compose.prod.yml`
  - [ ] Resource limits
  - [ ] Restart policies
  - [ ] Volume for Seq data persistence
- [ ] **OpenTelemetry Export**:
  - [ ] Configure OTLP exporter
  - [ ] Traces to Seq or Jaeger

---

## Phase 8 (Future Enhancements)

- [ ] Webhook mode for Telegram (when public endpoint available)
- [ ] Read/search Notion from Telegram ("what tasks are due this week?")
- [ ] Conversation memory (multi-turn within a topic)
- [ ] Web dashboard for viewing/editing entries
- [ ] Scheduled summaries (daily/weekly digests to Telegram)
- [ ] Multi-user support

---

## Prerequisites Setup Guide

Complete these steps before Phase 0.

### 1. Infisical Setup (Self-Hosted)

You have self-hosted Infisical. Create a project for DraftStream:

1. **Create Project**:
   - Go to your Infisical dashboard
   - Click "Add Project" → Name: `DraftStream`
   - Select environment: `dev` (create `prod` later)

2. **Add Secrets** (in `dev` environment):
   ```
   TELEGRAM_BOT_TOKEN=<from BotFather>
   TELEGRAM_GROUP_ID=<your group ID>
   TELEGRAM_TOPIC_NOTES=<topic ID>
   TELEGRAM_TOPIC_TASKS=<topic ID>
   TELEGRAM_TOPIC_SNIPPETS=<topic ID>
   OPENROUTER_API_KEY=<from openrouter.ai>
   NOTION_INTEGRATION_TOKEN=<from Notion>
   NOTION_DATABASE_NOTES=<database ID>
   NOTION_DATABASE_TASKS=<database ID>
   NOTION_DATABASE_SNIPPETS=<database ID>
   ```

3. **Create Service Token** (for the app to fetch secrets):
   - Project Settings → Service Tokens → Create
   - Scope: `dev` environment, read access
   - Save the token securely

4. **Note your Infisical URL**: `https://your-infisical-instance.com`

---

### 2. Telegram Setup

#### Create the Bot

1. Open Telegram, search for `@BotFather`
2. Send `/newbot`
3. Follow prompts:
   - Name: `DraftStream` (or your preference)
   - Username: `draftstream_bot` (must end in `bot`, must be unique)
4. **Save the bot token** (looks like `123456789:ABCdefGHI...`)

#### Create Group with Topics (Forum Mode)

1. **Create a new group** in Telegram:
   - Tap hamburger menu → "New Group"
   - Name: `DraftStream` (or your preference)
   - Add your bot as a member

2. **Enable Topics (Forum mode)**:
   - Open group → tap group name → Edit (pencil icon)
   - Scroll down → Enable "Topics"
   - Save

3. **Create Topics**:
   - In the group, tap "Create Topic"
   - Create three topics:
     - `Notes`
     - `Tasks`
     - `Snippets`

4. **Make bot an admin**:
   - Group settings → Administrators → Add Administrator
   - Select your bot
   - Enable: "Post messages", "Delete messages" (optional)

#### Get Group ID and Topic IDs

**Option A: Using @RawDataBot**
1. Add `@RawDataBot` to your group temporarily
2. Send a message in "General" topic → bot replies with JSON containing `chat.id` (this is your group ID, negative number)
3. Send a message in each topic (Notes, Tasks, Snippets) → note the `message.message_thread_id` for each
4. Remove @RawDataBot from group

**Option B: Using your bot's API**
Once your bot is running (Phase 1), logs will show the IDs.

**Typical values**:
- Group ID: `-1001234567890` (negative, starts with -100)
- Topic IDs: `2`, `3`, `4` (small positive integers, General is usually `1`)

---

### 3. Notion Setup

#### Create Integration

1. Go to [notion.so/my-integrations](https://www.notion.so/my-integrations)
2. Click "New integration"
3. Configure:
   - Name: `DraftStream`
   - Associated workspace: Your workspace
   - Capabilities: Read content, Insert content, Update content
4. **Save the Internal Integration Token** (starts with `secret_...`)

#### Create Databases

Create three databases in Notion with these schemas:

**Notes Database**:
| Property | Type | Notes |
|----------|------|-------|
| Title | Title | (default) |
| Content | Text | Rich text |
| Tags | Multi-select | Options: idea, reminder, reference, personal, work |
| Source | Select | Options: telegram |

**Tasks Database**:
| Property | Type | Notes |
|----------|------|-------|
| Title | Title | (default) |
| Description | Text | Rich text |
| Status | Status | Not started, In progress, Done |
| Priority | Select | Low, Medium, High, Urgent |
| Project | Select | (leave empty, AI will create options) |
| Labels | Multi-select | (leave empty, AI will create) |
| Due Date | Date | |

**Snippets Database**:
| Property | Type | Notes |
|----------|------|-------|
| Title | Title | (default) |
| Code | Text | Rich text |
| Language | Select | bash, sql, csharp, docker, git, python, javascript, other |
| Tags | Multi-select | (leave empty, AI will create) |
| Description | Text | Rich text |

#### Connect Integration to Databases

For **each** database:
1. Open the database page
2. Click `•••` (three dots) in top right
3. Click "Connections" → "Connect to" → Select `DraftStream`

#### Get Database IDs

For each database:
1. Open the database as a full page
2. Look at the URL: `https://notion.so/Your-Database-Name-abc123def456...`
3. The ID is the 32-character hex string (with dashes): `abc123de-f456-7890-abcd-ef1234567890`

---

### 4. OpenRouter Setup

1. Go to [openrouter.ai](https://openrouter.ai)
2. Sign in / Create account
3. Go to [openrouter.ai/keys](https://openrouter.ai/keys)
4. Create new key → Name: `DraftStream`
5. **Save the API key** (starts with `sk-or-...`)

**Recommended free models** (for MVP):
- `meta-llama/llama-3.1-8b-instruct:free`
- `google/gemma-2-9b-it:free`

---

## Prerequisites Checklist

- [ ] **Infisical**: Project `DraftStream` created, secrets added, service token generated
- [ ] **Telegram**:
  - [ ] Bot created via @BotFather, token saved
  - [ ] Group created with Topics enabled
  - [ ] Topics created: Notes, Tasks, Snippets
  - [ ] Bot added as admin
  - [ ] Group ID and Topic IDs noted
- [ ] **Notion**:
  - [ ] Integration created, token saved
  - [ ] Three databases created with correct schemas
  - [ ] Integration connected to all databases
  - [ ] Database IDs noted
- [ ] **OpenRouter**: API key saved

---

## Verification Strategy

After each phase, verify:

| Phase | Verification |
|-------|--------------|
| 0 | `docker-compose up` starts Seq, app logs appear in Seq UI |
| 1 | Send message in Telegram topic → log in Seq with topic ID |
| 2 | Send prompt to OpenRouter → receive structured response |
| 3 | App connects to MCP → list tools → create test page |
| 4 | Notes: Telegram → Notion → confirmation, trace in Seq |
| 5 | Tasks: Telegram → Notion with dates/priority → confirmation |
| 6 | Snippets: Code message → Notion with language → confirmation |
| 7 | Health endpoints respond, errors are user-friendly |
