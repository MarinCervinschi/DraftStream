# DraftStream

An AI-powered personal assistant that captures notes, tasks, and code snippets from Telegram and stores them in structured Notion databases — using OpenRouter for LLM processing and Notion MCP for database interaction.

## Concept

DraftStream is a **workflow-based agent**, not a generic chatbot. It uses a single Telegram bot deployed in a **group with topics** (Telegram Forum). Each topic acts as a dedicated workflow panel:

| Topic / Panel | Purpose | Notion Target |
|---|---|---|
| **Notes** | Quick thoughts, ideas, reminders | Notes database |
| **Tasks** | Work items, project tracking, deadlines | Tasks database |
| **Snippets** | CLI commands, code blocks, technical references | Snippets database |

Each panel has its own **system prompt** that instructs the LLM how to extract, classify, and structure the incoming message before writing it to Notion.

## Architecture

```
Telegram Group (with Topics)
    │
    ▼
┌──────────────┐
│ Telegram Bot  │  (.NET / Telegram.Bot)
│ (webhook/poll)│
└──────┬───────┘
       │  message + topic context
       ▼
┌──────────────┐
│  Agent Core   │  (.NET)
│               │
│ - Route by    │
│   topic       │
│ - Load panel  │
│   prompt      │
│ - Call LLM    │
└──────┬───────┘
       │  prompt + message
       ▼
┌──────────────┐
│  OpenRouter   │  (free/cheap models)
│  LLM API      │
│               │
│ Returns:      │
│ structured    │
│ data + tool   │
│ calls         │
└──────┬───────┘
       │  tool calls (create page, set properties)
       ▼
┌──────────────┐
│  Notion MCP   │  (MCP Server)
│  Server       │
│               │
│ - Create pages│
│ - Set props   │
│ - Add labels  │
└──────┬───────┘
       │
       ▼
   Notion Databases
   (Notes / Tasks / Snippets)
```

### Key Components

1. **Telegram Bot Layer** — Receives messages, identifies which topic/panel they belong to, and forwards them to the Agent Core.

2. **Agent Core** — The orchestrator. Routes messages to the correct workflow, loads the panel-specific system prompt, and calls OpenRouter. Handles the tool-call loop with the Notion MCP server.

3. **OpenRouter Client** — Sends prompts to free/cheap LLM models via the OpenRouter API. The model extracts structured information from the raw message and returns tool calls to store it.

4. **Notion MCP Client** — A .NET MCP client that connects to the Notion MCP server. Exposes Notion operations (create page, set properties) as tools that the LLM can invoke.

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# / .NET 8+ |
| Telegram | [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) NuGet package |
| LLM | [OpenRouter API](https://openrouter.ai/docs) (REST, OpenAI-compatible) |
| MCP Client | [ModelContextProtocol SDK for .NET](https://github.com/modelcontextprotocol/csharp-sdk) |
| Notion | [Notion MCP Server](https://github.com/makenotion/notion-mcp-server) (official, runs as stdio process) |
| Config | `appsettings.json` + user secrets for API keys |

## Notion Database Schemas

### Notes Database

| Property | Type | Description |
|---|---|---|
| Title | Title | Auto-generated short title from content |
| Content | Rich Text | The full note body |
| Tags | Multi-select | AI-assigned labels (e.g. `idea`, `reminder`, `reference`, `personal`, `work`) |
| Source | Select | Always `telegram` for now (future: `web`, `api`) |
| Created | Created time | Auto-set by Notion |

### Tasks Database

| Property | Type | Description |
|---|---|---|
| Title | Title | Task name |
| Description | Rich Text | Detailed task description |
| Status | Status | `Not started` / `In progress` / `Done` |
| Priority | Select | `Low` / `Medium` / `High` / `Urgent` |
| Project | Select | Project name (AI-extracted or user-specified) |
| Labels | Multi-select | Contextual tags |
| Due Date | Date | Deadline if mentioned |
| Created | Created time | Auto-set by Notion |

### Snippets Database

| Property | Type | Description |
|---|---|---|
| Title | Title | Short description of the command/snippet |
| Code | Rich Text | The actual command or code block |
| Language | Select | `bash`, `sql`, `csharp`, `docker`, `git`, `other` |
| Tags | Multi-select | Contextual tags (e.g. `kubernetes`, `database`, `debug`) |
| Description | Rich Text | When/why to use this snippet |
| Created | Created time | Auto-set by Notion |

## Workflow Example

**User sends in the "Tasks" topic:**
> Fix the login bug on the admin panel, high priority, project AdminPortal, due friday

**Agent processes:**
1. Telegram Bot detects message in "Tasks" topic
2. Agent Core loads the Tasks system prompt
3. LLM call via OpenRouter extracts: `{ title: "Fix login bug on admin panel", priority: "High", project: "AdminPortal", due: "2026-02-13" }`
4. MCP tool call → creates a Notion page in the Tasks database with those properties
5. Bot replies: "Task saved: **Fix login bug on admin panel** [High, AdminPortal, due Feb 13]"

## LLM Strategy (OpenRouter)

- Use **free-tier models** on OpenRouter where possible (e.g. `meta-llama/llama-3-8b-instruct:free`, `mistralai/mistral-7b-instruct:free`)
- Tasks are simple (text → structured JSON extraction), so small models work well
- Model is **configurable per panel** — a panel can override the default model if needed
- Fallback to a cheap paid model (e.g. Haiku) if free models are rate-limited or unavailable

## Project Structure (Planned)

```
DraftStream/
├── src/
│   ├── DraftStream.Agent/           # Main application / Agent Core
│   │   ├── Program.cs
│   │   ├── Workflows/              # Panel-specific workflow handlers
│   │   │   ├── NotesWorkflow.cs
│   │   │   ├── TasksWorkflow.cs
│   │   │   └── SnippetsWorkflow.cs
│   │   ├── Prompts/                # System prompts per panel
│   │   │   ├── notes-prompt.md
│   │   │   ├── tasks-prompt.md
│   │   │   └── snippets-prompt.md
│   │   └── Configuration/
│   │       └── PanelConfig.cs
│   ├── DraftStream.Telegram/       # Telegram bot integration
│   │   ├── TelegramBotService.cs
│   │   └── TopicRouter.cs
│   ├── DraftStream.OpenRouter/     # OpenRouter LLM client
│   │   ├── OpenRouterClient.cs
│   │   └── Models.cs
│   └── DraftStream.Mcp/           # MCP client for Notion
│       ├── NotionMcpClient.cs
│       └── ToolDefinitions.cs
├── tests/
│   └── DraftStream.Tests/
├── DraftStream.sln
├── README.md
├── CLAUDE.md
└── .gitignore
```

## Configuration

Required API keys (stored in .NET User Secrets or environment variables, **never** in source):

| Key | Source |
|---|---|
| `Telegram:BotToken` | [@BotFather](https://t.me/BotFather) on Telegram |
| `OpenRouter:ApiKey` | [openrouter.ai/keys](https://openrouter.ai/keys) |
| `Notion:IntegrationToken` | [Notion Integrations](https://www.notion.so/my-integrations) |

## Phase 1 — MVP Scope

- [ ] .NET solution scaffolding
- [ ] Telegram bot with topic-based routing
- [ ] OpenRouter client (free models, OpenAI-compatible API)
- [ ] Notion MCP client integration
- [ ] Notes workflow (send text → saves structured note in Notion)
- [ ] Tasks workflow (send text → saves structured task in Notion)
- [ ] Snippets workflow (send code/command → saves structured snippet in Notion)
- [ ] Confirmation messages back to Telegram
- [ ] Error handling and retry logic

## Future Ideas

- **Read/search** — query Notion from Telegram ("what tasks are due this week?")
- **Web frontend** — dedicated UI beyond Telegram
- **More workflows** — bookmarks, journal entries, meeting notes
- **Scheduled summaries** — daily/weekly digests sent to Telegram
- **Multi-user support**
