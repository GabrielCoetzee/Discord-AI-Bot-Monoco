# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build

# Run
dotnet run --project MonocoBot

# Test
dotnet test

# Publish (release)
dotnet publish MonocoBot --configuration Release --output ./publish
```

## Configuration

Secrets are managed via .NET User Secrets (ID: `monocobot-dev`):
```bash
dotnet user-secrets set "Bot:DiscordToken" "your-token" --project MonocoBot
dotnet user-secrets set "Bot:AiApiKey" "your-key" --project MonocoBot
```

Environment variables use double-underscore prefix: `Bot__DiscordToken`, `Bot__AiProvider`, etc.

Key settings in `BotOptions`: Name, DiscordToken, AiProvider (`openai`|`azure`|`ollama`), AiModel, AiApiKey, AiEndpoint, AiTemperature, HealthPort (8080), MaxConversationHistory (50), IsThereAnyDealApiKey.

## Architecture

**Discord bot** built on .NET 10, Discord.Net, and Microsoft.Extensions.AI / Microsoft.SemanticKernel with pluggable AI providers.

### Core Flow

`Program.cs` sets up DI and the web host. `DiscordBotService` (IHostedService) connects to Discord, listens for mentions/DMs, and orchestrates the response pipeline by delegating to extracted services. Services use interface-based DI — see the **Service lifetimes** rule under Strict Rules for guidance on choosing the correct lifetime when adding new services.

### Services (`MonocoBot/Services/`)

- **DiscordBotService** — Main hosted service. Handles Discord events, orchestrates message processing by delegating to the services below.
- **ChatClientFactory** — Static factory that creates `IChatClient` for the configured AI provider (OpenAI, Azure, Ollama) with Semantic Kernel's `FunctionInvocationChatClient` middleware for automatic tool invocation.
- **SystemPromptProvider** (`ISystemPromptProvider`) — Loads and caches the system prompt from `Resources/system-prompt.txt`, replacing `{BotName}` with the configured bot name.
- **ConversationHistoryManager** (`IConversationHistoryManager`) — Thread-safe per-channel conversation history using `ConcurrentDictionary<ulong, List<ChatMessage>>`. Handles creation, appending, trimming, and clearing.
- **MessageContentProcessor** (`IMessageContentProcessor`) — Strips bot mentions, resolves user mentions to display names, and extracts author display names.
- **MessageSplitter** — Static utility that splits messages exceeding Discord's 2000-char limit on newline boundaries.
- **DiscordMessageSender** (`IMessageSender`) — Sends AI responses back to Discord, handling message splitting and file attachments with automatic temp file cleanup.
- **AiToolRegistry** (`IAiToolRegistry`) — Centralizes AI tool registration. Creates `AIFunction` wrappers via `AIFunctionFactory` for all tool classes and exposes them via `GetTools()`.

### AI Tool System (`MonocoBot/Tools/`)

Tool classes are registered as singletons in DI and wrapped by `AiToolRegistry`. Methods use `[Description]` attributes for AI discovery. The Semantic Kernel `FunctionInvocationChatClient` middleware handles automatic tool invocation.

Available tools:
- **PdfTools** — `CreatePdf`: Generates A4 PDFs with markdown-like formatting (QuestPDF).
- **CodeRunnerTools** — `RunCSharpCode`: Executes C# scripts with a 30-second timeout (Roslyn scripting).
- **WebSearchTools** — `SearchWeb` (DuckDuckGo HTML scraping), `ReadWebPage` (HTML fetch + cleanup, 4000-char limit).
- **SteamTools** — `GetLocalProfileData` (reads `steam_profiles.json`), `LookupGameDeals` (IsThereAnyDeal API), `LookupSteamPrice` (Steam Store API).
- **DateTimeTools** — `GetCurrentDateTime`, `ConvertTimezone`.
- **WeatherTools** — `GetCurrentWeather`, `GetWeatherForecast` (Open-Meteo API, 1–7 day forecasts).
- **CurrencyHelper** — Static utility for price formatting, Steam country codes, and exchange rates (`open.er-api.com`).

`ToolOutput` is an `AsyncLocal`-based thread-safe file queue — tools enqueue files, and `DiscordMessageSender` dequeues them after the AI response.

### Models (`MonocoBot/Models/`)

DTOs for external APIs, organized by domain:
- `Steam/SteamModels.cs` — Steam Store API response types.
- `Steam/ItadModels.cs` — IsThereAnyDeal API response types.
- `Weather/WeatherModels.cs` — Open-Meteo geocoding and weather response types.

### Constants & Resources

- `Constants.cs` — `DiscordMaxMessageLength` (2000), `BotUserAgent`, `BrowserUserAgent`.
- `Resources/system-prompt.txt` — Bot personality prompt defining "Monoco" from Clair Obscur: Expedition 33. Copied to output directory at build time.

## Testing

`MonocoBot.Tests/` — xUnit tests with NSubstitute for mocks and Flurl.Http test fixtures for HTTP mocking.

Covers: ConversationHistoryManager, MessageSplitter, MessageContentProcessor, SystemPromptProvider, DateTimeTools, CurrencyHelper, SteamTools, WeatherTools, WebSearchTools.

## Deployment

Deployed to Azure Web App ("Monoco-discord-bot") via GitHub Actions (`.github/workflows/main_monoco-discord-bot.yml`). Builds on ubuntu-latest with .NET 10.x, deploys via Azure OIDC.


## Code Conventions

- **Separation of abstractions and implementations:** Interfaces and base classes must never share a file with concrete implementations. Place them in separate folders — `Abstract/` for interfaces and base classes, `Concrete/` for implementations. For example: `Services/Abstract/IMessageSender.cs` and `Services/Concrete/DiscordMessageSender.cs`.

- **Vertical spacing:** Separate logically distinct lines or blocks with a blank line to aid readability. For example, always follow a variable declaration with a blank line before the next logical statement.

## Strict Rules

- **Service lifetimes:** Do not default to `.AddSingleton` when registering new classes. Evaluate the appropriate lifetime (`Singleton`, `Scoped`, or `Transient`) on a case-by-case basis.

  In this application, all current services are correctly registered as Singletons. The root of the DI graph is `DiscordBotService` (a hosted service, which the runtime registers as Singleton). Any service injected into a Singleton must itself be Singleton to avoid captive dependencies — a shorter-lived instance held permanently inside a longer-lived one. Additionally, this is a Discord bot with no HTTP request scope, so `Scoped` has no natural boundary here. The rule still applies when adding new services: if a service is not a dependency of an existing Singleton and has no shared state, prefer `Transient`.

- **Comments:** Do not add comments unless they explain something that cannot be made clear through the code itself.