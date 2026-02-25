# DraftStream

AI-powered agent that captures notes, tasks, and code snippets from Telegram and stores them in Notion databases — using OpenRouter for LLM processing and Notion MCP for database interaction.

## Architecture

DraftStream is a **workflow-based agent**, not a chatbot. It uses a Telegram group with topics (Forum). Each topic is a dedicated workflow panel (Notes, Tasks, Snippets) with its own system prompt and Notion target database.

```
Telegram → Topic Routing → Panel Prompt → OpenRouter LLM → MCP Tool Calls → Notion
```

## Tech Stack

- **C# / .NET 9** — Worker service
- **Telegram.Bot** — Telegram bot SDK
- **OpenRouter API** — LLM (free/cheap models, OpenAI-compatible)
- **Notion MCP Server** — official `@notionhq/notion-mcp-server` (stdio)
- **Infisical** — secrets management
- **Serilog + Seq** — structured logging
- **OpenTelemetry** — tracing

## Project Structure

```
DraftStream/
├── src/
│   ├── DraftStream.Domain/              # Enums, value objects
│   ├── DraftStream.Application/         # Interfaces, shared contracts
│   ├── DraftStream.Application.Notes/   # Notes workflow handler
│   ├── DraftStream.Application.Tasks/   # Tasks workflow handler
│   ├── DraftStream.Application.Snippets/# Snippets workflow handler
│   ├── DraftStream.Infrastructure/      # Infisical, Serilog, OpenTelemetry, external integrations
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

The LLM client connects to OpenRouter's OpenAI-compatible API with built-in resilience (retry on 429/5xx, circuit breaker, timeouts via Polly).

| Setting | Location | Description |
|---|---|---|
| `OpenRouter__ApiKey` | Infisical | API key from [openrouter.ai/keys](https://openrouter.ai/keys) |
| `OpenRouter:DefaultModel` | appsettings.json | Default model for all workflows |
| `OpenRouter:ModelOverrides` | appsettings.json | Per-workflow model overrides (e.g., `{ "notes": "google/gemma-2-9b-it:free" }`) |

## Current Status

- **Phase 2** — OpenRouter LLM client with OpenAI-compatible chat completion and tool/function calling support. Typed HttpClient with Polly resilience (retry, circuit breaker, timeouts). OpenTelemetry tracing on LLM calls.
- **Phase 1** — Telegram bot integration with message source abstraction. Bot receives messages via long polling, routes by topic to workflow handlers. Extensible to support additional message sources (Discord, webhooks, etc.).
- **Phase 0** — Solution scaffolding, Clean Architecture, Infisical integration, Serilog/Seq logging, OpenTelemetry tracing, Docker setup.
