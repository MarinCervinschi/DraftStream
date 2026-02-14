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

## Code Quality Guidelines

### SOLID Principles

All code must follow SOLID principles:

- **Single Responsibility** — each class and method does one thing and has one reason to change.
- **Open/Closed** — extend behavior through abstractions (interfaces, inheritance), not by modifying existing code.
- **Liskov Substitution** — subtypes must be usable wherever their base type is expected, without surprises.
- **Interface Segregation** — prefer small, focused interfaces over large ones. Clients should not depend on methods they don't use.
- **Dependency Inversion** — depend on abstractions, not concrete implementations. Use constructor injection via the built-in DI container.

### Modularity & Scalability

- Design components to be independently testable and replaceable.
- Keep clear boundaries between layers (e.g., Telegram handling, LLM orchestration, Notion integration).
- Favor composition over inheritance.
- Extract shared logic into well-named services only when reuse is real, not hypothetical.

### Readability & Self-Descriptive Code

- Code should be understandable by any developer, even without prior knowledge of the project.
- Use meaningful, descriptive names for classes, methods, variables, and parameters.
- Keep methods short and focused — if a method needs a comment to explain *what* it does, rename or refactor it instead.
- Add comments only to explain *why* something is done, never *what* — the code itself should make the "what" obvious.
- Follow consistent C# naming conventions (PascalCase for public members, camelCase for locals, `_camelCase` for private fields).

### Scope Discipline

- **Implement only what is explicitly requested.** Do not add features, utilities, or abstractions speculatively.
- If a potential improvement or feature comes to mind, **ask first** before implementing. Never build something that won't be used.
- Avoid premature optimization and over-engineering. Solve today's problem, not tomorrow's hypothetical one.
- When fixing a bug, fix the bug — don't refactor surrounding code unless asked.

### Error Handling & Exceptions

- **Use try/catch in critical methods** — external API calls (Telegram, OpenRouter, Notion), message processing pipelines, and startup/configuration logic. Not every method needs a try/catch; analyse the situation and decide whether a failure here would be hard to diagnose without explicit handling.
- **Don't wrap trivial or pure logic** — simple mappings, validations, and in-memory transformations that can only fail from programming errors don't need try/catch. Let those bubble up naturally.
- **Every exception message must be unique and specific** — describe the exact operation that failed and the relevant context. Never use generic messages like "An error occurred" or copy the same message across different catch blocks.
- **Log before re-throwing** — use `ILogger` with relevant state (parameters, IDs, operation name) so failures are traceable. Use `LogError` for unexpected failures, `LogWarning` for recoverable ones.
- **Preserve the original exception** — always pass the caught exception as inner exception: `throw new XException("message", ex)`.
- **Use specific exception types** — catch the most specific type that fits (`HttpRequestException`, `InvalidOperationException`, etc.). Bare `Exception` catches are only acceptable in top-level entry points (message handlers, background jobs).
- **Think before adding a try/catch** — ask: "If this fails, will the caller or logs give enough context to debug it?" If yes, a try/catch here adds noise. If no, add one with a meaningful message.

### General Practices

- Write unit tests for new logic; follow existing test patterns and conventions in the project.
- Use `async/await` consistently for I/O-bound operations.
- Keep dependencies minimal — add a NuGet package only when it provides clear value over a simple implementation.
