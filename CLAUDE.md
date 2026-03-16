# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build

# Run
dotnet run --project MonocoBot

# Publish (release)
dotnet publish MonocoBot --configuration Release --output ./publish
```

There are no test projects or linting configurations in this repo.

## Configuration

Secrets are managed via .NET User Secrets (ID: `monocobot-dev`):
```bash
dotnet user-secrets set "Bot:DiscordToken" "your-token" --project MonocoBot
dotnet user-secrets set "Bot:AiApiKey" "your-key" --project MonocoBot
```

Environment variables use double-underscore prefix: `Bot__DiscordToken`, `Bot__AiProvider`, etc.

Key settings in `BotOptions`: Name, DiscordToken, AiProvider (`openai`|`azure`|`ollama`), AiModel, AiApiKey, AiEndpoint, AiTemperature (0.7), HealthPort (8080), MaxConversationHistory (50), IsThereAnyDealApiKey.

## Architecture

**Discord bot** built on .NET 10, Discord.Net, and Microsoft.SemanticKernel with pluggable AI providers.

### Core Flow

`Program.cs` sets up DI and the web host. `DiscordBotService` (IHostedService) is the main service — it connects to Discord, listens for mentions/DMs, manages per-channel conversation history (`ConcurrentDictionary<ulong, List<ChatMessage>>`), and calls the AI chat client with registered tools.

### AI Tool System

Tools live in `MonocoBot/Tools/` as static methods with `[Description]` attributes, registered via `AIFunctionFactory` in the `DiscordBotService` constructor. The Semantic Kernel `FunctionInvocationChatClient` middleware handles automatic tool invocation.

Available tools: CreatePdf, RunCSharpCode, SearchWeb, ReadWebPage, GetLocalProfileData, LookupGameDeals, LookupSteamPrice, GetCurrentDateTime, ConvertTimezone, GetCurrentWeather, GetWeatherForecast.

`ToolOutput` is a static thread-safe file attachment queue — tools enqueue files, and the message handler dequeues them after the AI response.

### Bot Personality

The system prompt in `DiscordBotService` defines the bot as "Monoco," a character from Clair Obscur: Expedition 33. This is central to the bot's identity and response style.

### Message Handling

Messages over 2000 characters (Discord limit) are split on newline boundaries. User mentions in messages are resolved to display names before being sent to the AI.

## Deployment

Deployed to Azure Web App via GitHub Actions (`.github/workflows/main_monoco-discord-bot.yml`). Also supports Docker and systemd.
