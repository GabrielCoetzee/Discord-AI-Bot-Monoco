# Monoco - Discord - Bot 🤖

MonocoBot is an AI-powered Discord bot built with .NET 10. It uses large language models (OpenAI, Azure OpenAI, or Ollama) combined with [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) to provide a conversational assistant with tool-calling capabilities — all wrapped in the charming personality of **Monoco**, the adorable companion from *Expedition 33*.

Mention the bot or DM it and it will respond with context-aware, multi-turn conversation powered by your chosen AI provider.

## Features

- **Multi-provider AI** — Supports OpenAI, Azure OpenAI (Azure AI Foundry), and local Ollama models.
- **Conversational memory** — Maintains per-channel conversation history with configurable limits. Say `clear` or `reset` to start fresh.
- **Steam integration** — Look up game libraries, wishlists, friends lists, and resolve vanity URLs via the Steam Web API. Users can register personal API keys to access private profiles.
- **PDF generation** — Create formatted PDF documents (headings, bullet lists, paragraphs) using QuestPDF. Files are automatically attached to the reply.
- **C# code execution** — Run C# snippets in a sandboxed Roslyn scripting environment with a 30-second timeout.
- **Web search & page reading** — Search the web via DuckDuckGo and fetch the text content of any public URL.
- **Date/time utilities** — Get the current time in any timezone or convert between timezones.
- **Long message handling** — Automatically splits responses that exceed Discord's 2000-character limit.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A Discord bot token ([Discord Developer Portal](https://discord.com/developers/applications))
- An AI provider API key — **one** of:
  - [OpenAI API key](https://platform.openai.com/account/api-keys)
  - [Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/) endpoint + key
  - A running [Ollama](https://ollama.com/) instance (no key required)
- *(Optional)* A [Steam Web API key](https://steamcommunity.com/dev/apikey) for Steam features

## Configuration

The bot reads settings from `appsettings.json` and supports [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for local development (secrets ID: `monocobot-dev`).

### appsettings.json

```json
{
  "Bot": {
    "Name": "Monoco",
    "DiscordToken": "",
    "AiProvider": "openai",
    "AiModel": "gpt-4o-mini",
    "AiApiKey": "",
    "AiEndpoint": "",
    "SteamApiKey": "",
    "MaxConversationHistory": 50
  }
}
```

| Setting | Description |
|---|---|
| `Name` | The bot's display name used in the system prompt. |
| `DiscordToken` | Your Discord bot token. |
| `AiProvider` | `"openai"`, `"azure"`, or `"ollama"`. |
| `AiModel` | The model/deployment name (e.g., `gpt-4o-mini`). |
| `AiApiKey` | API key for OpenAI or Azure OpenAI. Not needed for Ollama. |
| `AiEndpoint` | Required for Azure and Ollama (e.g., `http://localhost:11434/v1`). |
| `SteamApiKey` | *(Optional)* Global Steam Web API key. |
| `MaxConversationHistory` | Max messages retained per channel (default: `50`). |

### Using User Secrets (recommended for development)

```bash
cd MonocoBot
dotnet user-secrets set "Bot:DiscordToken" "your-discord-token"
dotnet user-secrets set "Bot:AiApiKey" "your-api-key"
dotnet user-secrets set "Bot:SteamApiKey" "your-steam-key"
```

## Building & Running

```bash
# Restore dependencies and build
dotnet build

# Run the bot
dotnet run --project MonocoBot
```

The bot will log in to Discord and print a message like:

```
Monoco is online — connected to 3 server(s).
```

## Debugging

### Visual Studio

1. Open the solution in Visual Studio.
2. Make sure `MonocoBot` is set as the startup project.
3. Set your secrets via **right-click project → Manage User Secrets** or the `dotnet user-secrets` CLI.
4. Press **F5** to start debugging. Breakpoints, watch windows, and the Output window all work as expected.

### VS Code

1. Open the repository folder.
2. Ensure the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension is installed.
3. Configure secrets as described above.
4. Use the **Run and Debug** panel (`Ctrl+Shift+D`) and select the .NET launch configuration.

### CLI

```bash
dotnet run --project MonocoBot
```

Attach a debugger to the running process if needed, or add `Debugger.Launch()` temporarily.

## Deployment

### As a systemd service (Linux)

1. Publish a self-contained build:

   ```bash
   dotnet publish MonocoBot -c Release -o ./publish
   ```

2. Copy the `publish` folder to your server.

3. Create a systemd unit file (`/etc/systemd/system/monocobot.service`):

   ```ini
   [Unit]
   Description=MonocoBot Discord Bot
   After=network.target

   [Service]
   WorkingDirectory=/opt/monocobot
   ExecStart=/opt/monocobot/MonocoBot
   Restart=always
   RestartSec=10
   Environment=Bot__DiscordToken=your-token
   Environment=Bot__AiApiKey=your-key
   Environment=Bot__AiProvider=openai

   [Install]
   WantedBy=multi-user.target
   ```

4. Enable and start:

   ```bash
   sudo systemctl enable monocobot
   sudo systemctl start monocobot
   ```

### As a Docker container

Create a `Dockerfile` in the repository root:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish MonocoBot -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./MonocoBot"]
```

Build and run:

```bash
docker build -t monocobot .
docker run -d --name monocobot \
  -e Bot__DiscordToken=your-token \
  -e Bot__AiApiKey=your-key \
  -e Bot__AiProvider=openai \
  -e Bot__AiModel=gpt-4o-mini \
  monocobot
```

### On Windows

Publish and run as a Windows Service, a scheduled task, or simply run the executable directly:

```bash
dotnet publish MonocoBot -c Release -o ./publish
./publish/MonocoBot.exe
```

Set configuration via environment variables prefixed with `Bot__` (double underscore), e.g. `Bot__DiscordToken`.

## Project Structure

```
MonocoBot/
├── Configuration/
│   └── BotOptions.cs            # Strongly-typed configuration model
├── Services/
│   └── DiscordBotService.cs     # Hosted service — Discord connection, message handling, AI orchestration
├── Tools/
│   ├── CodeRunnerTools.cs       # Roslyn-based C# code execution
│   ├── DateTimeTools.cs         # Timezone queries and conversion
│   ├── PdfTools.cs              # PDF document generation (QuestPDF)
│   ├── SteamKeyStore.cs         # Per-user Steam API key persistence
│   ├── SteamTools.cs            # Steam Web API integration
│   ├── ToolOutput.cs            # Thread-safe file attachment pipeline
│   └── WebSearchTools.cs        # DuckDuckGo search and web page reader
├── appsettings.json             # Default configuration
├── steam_profiles.json          # Manual game data for private profiles
├── Program.cs                   # Host builder, DI setup, AI provider selection
└── MonocoBot.csproj             # Project file (.NET 10)
```

## License

See the repository for license information.
