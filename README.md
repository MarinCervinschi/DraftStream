# DraftStream

AI-powered agent that captures notes, tasks, and code snippets from Telegram and stores them in Notion databases — using OpenRouter for LLM processing and Notion MCP for database interaction.

## Architecture

DraftStream is a **workflow-based agent**, not a chatbot. It uses a Telegram group with topics (Forum). Each topic is a dedicated workflow panel (Notes, Tasks, Snippets) with its own system prompt and Notion target database.

```
Telegram → Topic Routing → Panel Prompt → IChatClient (OpenRouter) → FunctionInvokingChatClient → MCP Tools → Notion
```

## Tech Stack

- **C# / .NET 9** — Worker service
- **Telegram.Bot** — Telegram bot SDK
- **Microsoft.Extensions.AI** — `IChatClient` with automatic tool invocation middleware
- **OpenRouter API** — LLM (free/cheap models, OpenAI-compatible)
- **ModelContextProtocol SDK** — MCP client (`McpClientTool` → `AIFunction` integration)
- **Notion MCP Server** — `@notionhq/notion-mcp-server` (stdio)
- **Infisical** — secrets management
- **Serilog + Seq** — structured logging
- **OpenTelemetry** — tracing

## Project Structure

```
DraftStream/
├── src/
│   ├── DraftStream.Domain/              # Enums, value objects
│   ├── DraftStream.Application/         # Interfaces, workflows, prompts, shared contracts
│   │   ├── Fallback/                    # IFallbackStorage
│   │   ├── Mcp/                         # IMcpToolProvider, McpToolResult
│   │   ├── Messaging/                   # IMessageSource, IMessageDispatcher, IncomingMessage
│   │   ├── Workflows/                   # SchemaWorkflowHandler, WorkflowSettings
│   │   └── Prompts/                     # PromptBuilder, notes.md, tasks.md, snippets.md
│   ├── DraftStream.Infrastructure/      # Infisical, Serilog, OpenTelemetry, external integrations
│   │   ├── Messaging/                   # MessageDispatcher, MessageSourceBackgroundService
│   │   ├── Notion/                      # NotionMcpClient, NotionFallbackStorage
│   │   ├── OpenRouter/                  # OpenRouterSettings
│   │   └── Telegram/                    # TelegramMessageSource, TelegramSettings
│   └── DraftStream.Host/               # Composition root (worker service)
├── Directory.Build.props                # Shared build properties
├── Dockerfile                           # Multi-stage build
└── docker-compose.yml                   # App + Seq
```

## Quick Start

```bash
# Build
dotnet build

# Run locally (Infisical credentials optional for now)
dotnet run --project src/DraftStream.Host

# Run with Docker (requires .env with Infisical credentials)
docker-compose up --build

# Seq UI (structured logs)
# http://localhost:5341
```

## Configuration

Secrets are managed via **Infisical** (Universal Auth). Without credentials, the app starts normally but skips secret loading.

| Variable | Where | Required |
|---|---|---|
| `Infisical:ProjectId` | appsettings.json | For Infisical |
| `Infisical:Environment` | appsettings.json | For Infisical |
| `Infisical:ClientId` | env var / `.env` | For Infisical |
| `Infisical:ClientSecret` | env var / `.env` | For Infisical |

Application secrets (stored in Infisical, loaded at startup):
- `Telegram__BotToken`
- `OpenRouter__ApiKey`
- `Notion__IntegrationToken`

## Docker

```bash
# Create .env file with Infisical credentials
cat > .env <<EOF
INFISICAL_CLIENT_ID=your-client-id
INFISICAL_CLIENT_SECRET=your-client-secret
INFISICAL_PROJECT_ID=your-project-id
INFISICAL_ENVIRONMENT=dev
EOF

docker-compose up --build
```

## Telegram Configuration

The bot connects to a Telegram group with Topics (Forum mode). Each topic maps to a workflow:

| Infisical Secret | Description |
|---|---|
| `Telegram__BotToken` | Bot token from @BotFather |
| `Telegram__GroupId` | Telegram group ID (negative number) |
| `Telegram__TopicMappings__<threadId>` | Maps topic thread ID to workflow name (`notes`, `tasks`, `snippets`) |

Example Infisical secrets:
```
Telegram__BotToken=123456789:ABCdefGHI...
Telegram__GroupId=-1001234567890
Telegram__TopicMappings__2=notes
Telegram__TopicMappings__3=tasks
Telegram__TopicMappings__4=snippets
```

## OpenRouter Configuration

The LLM client uses `IChatClient` from `Microsoft.Extensions.AI` backed by `Microsoft.Extensions.AI.OpenAI` pointing at OpenRouter's endpoint. HTTP resilience (retry on 429/5xx, circuit breaker, timeouts) via Polly. The `FunctionInvokingChatClient` middleware handles the tool invocation loop automatically.

| Setting | Location | Description |
|---|---|---|
| `OpenRouter__ApiKey` | Infisical | API key from [openrouter.ai/keys](https://openrouter.ai/keys) |
| `OpenRouter:DefaultModel` | appsettings.json | Default model for all workflows |

## Notion MCP Configuration

DraftStream connects to Notion via the official MCP server (`@notionhq/notion-mcp-server`), spawned as a stdio child process. **Requires Node.js/npx** on the host.

| Setting | Location | Description |
|---|---|---|
| `Notion__IntegrationToken` | Infisical | Notion integration token (starts with `ntn_...`) |

The MCP server process starts lazily on first use and reconnects automatically on failure.

## Workflow Configuration

Each workflow (Notes, Tasks, Snippets) is configured in `appsettings.json` under `Workflows:Items`. The handler dynamically fetches the Notion database schema via a two-step MCP call (database → data source) — **no code changes needed** when you add or remove database columns. Schemas are cached for 30 minutes and automatically refetched after expiry.

| Setting | Location | Description |
|---|---|---|
| `Workflows:Items:<name>:DatabaseId` | appsettings.json / Infisical | Notion database ID for the workflow |
| `Workflows:Items:<name>:ModelOverride` | appsettings.json | Optional LLM model override for this workflow |

Example:
```json
{
  "Workflows": {
    "Items": {
      "notes": { "DatabaseId": "abc123..." },
      "tasks": { "DatabaseId": "def456...", "ModelOverride": "google/gemma-2-9b-it:free" },
      "snippets": { "DatabaseId": "ghi789..." }
    }
  }
}
```

## Fallback Storage

If the LLM workflow fails (model error, MCP timeout, etc.), the system automatically saves the raw message directly to the Notion database via the `API-post-page` MCP tool — bypassing the LLM entirely. The user receives a reply indicating whether the fallback save succeeded or not. This ensures no messages are lost even during outages.

## Current Status

- **Post-Phase 7** — Schema cache upgraded from static `ConcurrentDictionary` to `IMemoryCache` with 30-minute TTL. Notion schema changes are picked up automatically without app restart. Added fallback storage (`IFallbackStorage` / `NotionFallbackStorage`) to save raw messages directly to Notion when LLM processing fails, with `sourceType` context propagation across the pipeline.
- **Phase 7** — Replaced manual agentic tool loop with `Microsoft.Extensions.AI` SDK integration. `IChatClient` with `FunctionInvokingChatClient` middleware handles tool invocation automatically. `McpClientTool` (inherits `AIFunction`) plugs directly into `ChatOptions.Tools`. Removed ~400 lines of custom LLM client, API models, and tool loop code. Updated Notion MCP schema fetch to two-step data source flow (`API-retrieve-a-database` → `API-retrieve-a-data-source`).
- **Phases 4-6** — Schema-driven workflow engine. Single generic handler for all workflows (Notes, Tasks, Snippets). Dynamically fetches Notion database schema, injects it into the LLM prompt, and lets the LLM fill properties based on actual columns. Reply mechanism for confirmation messages. Zero code changes when Notion schema changes.
- **Phase 3** — Notion MCP client via `ModelContextProtocol` SDK. Spawns `@notionhq/notion-mcp-server` as stdio child process. Lazy init, thread-safe, reconnect-on-failure, tool caching. OpenTelemetry tracing on MCP operations.
- **Phase 2** — OpenRouter LLM via `IChatClient` pipeline with HTTP resilience (Polly: retry, circuit breaker, timeouts).
- **Phase 1** — Telegram bot integration with message source abstraction. Bot receives messages via long polling, routes by topic to workflow handlers. Extensible to support additional message sources (Discord, webhooks, etc.).
- **Phase 0** — Solution scaffolding, Clean Architecture, Infisical integration, Serilog/Seq logging, OpenTelemetry tracing, Docker setup.
