# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DraftStream is a .NET 8+ AI agent that captures notes, tasks, and code snippets from Telegram and stores them in Notion databases. It uses OpenRouter for LLM processing and the official Notion MCP server for database interaction.

## Architecture

- **Workflow-based, not a chatbot** — each Telegram topic is a separate workflow panel (Notes, Tasks, Snippets) with its own system prompt and Notion target database.
- **Flow**: Telegram message → topic routing → panel-specific prompt → OpenRouter LLM → MCP tool calls → Notion database write → Telegram confirmation.
- **MCP pattern**: The agent acts as an MCP client connecting to the Notion MCP server (stdio process). The LLM decides which MCP tools to call based on the extracted data.

## Tech Stack

- **C# / .NET 8+**
- **Telegram.Bot** — Telegram bot SDK
- **OpenRouter API** — OpenAI-compatible REST API for LLM (targeting free/cheap models)
- **ModelContextProtocol SDK for .NET** — MCP client
- **Notion MCP Server** — official Notion MCP server (`@notionhq/notion-mcp-server`, runs as stdio)

## Build & Run

```bash
dotnet build src/DraftStream.Agent
dotnet run --project src/DraftStream.Agent
dotnet test tests/DraftStream.Tests
```

## Configuration

API keys are stored in .NET User Secrets (never committed):
- `Telegram:BotToken`
- `OpenRouter:ApiKey`
- `Notion:IntegrationToken`

## Key Design Decisions

- OpenRouter is used instead of direct model APIs to access free-tier models and easily switch between them.
- Each panel/workflow can override the default LLM model if needed.
- The LLM's job is simple structured extraction (text → JSON properties), so small/free models suffice.
- Write-only to Notion for MVP; read/search comes later.
