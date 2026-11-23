# MAF AgentLab

An interactive **.NET 11 console application** containing nine demos for the
[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/).
The shared demo logic lives in a UI-independent library. The console displays
selection menus, live status, agents, responses, and trace events.

![Agent](/docs/header.png)

## Run

Install **.NET SDK 11.0.100** or a later patch in the feature band selected by
`global.json`.

```powershell
dotnet restore MAFAgentLab.slnx
dotnet run --project src/MAFAgentLab

# Run a single non-interactive replay:
dotnet run --project src/MAFAgentLab -- --replay 03
```

The UI is fully keyboard-driven. A live run can be cancelled with `Ctrl+C`.
Guided Replay runs all nine examples without making a model call or requiring
credentials.

## Configure providers with Visual Studio User Secrets

The application loads configuration in this order:

1. `appsettings.json`
2. Visual Studio User Secrets
3. Environment variables

Later sources override earlier values. The console project already contains a
`UserSecretsId`, so secrets remain outside the repository.

### Azure OpenAI

```powershell
dotnet user-secrets set --project src/MAFAgentLab "AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com/"
dotnet user-secrets set --project src/MAFAgentLab "AzureOpenAI:ApiKey" "<api-key>"
dotnet user-secrets set --project src/MAFAgentLab "AzureOpenAI:Deployment" "<chat-deployment>"
```

### Ollama

The default local address, `http://localhost:11434`, is already configured in
`appsettings.json`.

```powershell
dotnet user-secrets set --project src/MAFAgentLab "Ollama:Deployment" "<model-from-ollama-list>"
```

An API key is required only for an authenticated proxy:

```powershell
dotnet user-secrets set --project src/MAFAgentLab "Ollama:ApiKey" "<proxy-token>"
```

Because the console application connects directly to Ollama, `localhost` refers
to the same machine as the process. Use the native Ollama base address without
`/api` or `/v1`, and use the exact model name shown by `ollama list`.

### OpenAI

The official OpenAI API requires an API key and model:

```powershell
dotnet user-secrets set --project src/MAFAgentLab "OpenAI:ApiKey" "<api-key>"
dotnet user-secrets set --project src/MAFAgentLab "OpenAI:Model" "<chat-model>"
```

For an OpenAI-compatible endpoint, also set the base URL including `/v1`:

```powershell
dotnet user-secrets set --project src/MAFAgentLab "OpenAI:Endpoint" "https://<host>/v1"
```

The provider for a normal demo is selected from the menu. With `Auto`, the
application uses the first complete provider in this order:

1. Azure OpenAI
2. Ollama
3. OpenAI

Demo 03 uses **every completely configured provider** with the same prompt.
Incomplete providers are skipped. If no configured provider succeeds, the run
ends with an error.

### Tool demo

Demo 04 additionally requires an OpenWeatherMap key:

```powershell
dotnet user-secrets set --project src/MAFAgentLab "OpenWeatherMap:ApiKey" "<api-key>"
```

Environment variable equivalents use double underscores. Examples include
`AzureOpenAI__Endpoint`, `Ollama__Deployment`, and `OpenWeatherMap__ApiKey`.

## Demos

| ID | Example | Framework concept |
| --- | --- | --- |
| 01 | Simple agent | `IChatClient.AsAIAgent`, instructions, and streaming |
| 02 | Conversation and persistence | Serialize and restore an `AgentSession` |
| 03 | Compare providers | The same agent with Azure OpenAI, Ollama, and OpenAI |
| 04 | Agents with tools | Model-selected C# functions |
| 05 | Sequential workflow | Accumulated context across four specialists |
| 06 | Concurrent workflow | Fan-out/fan-in with separate perspectives |
| 07 | Handoff workflow | Model-driven routing through explicit routes |
| 08 | Group chat | Round-robin conversation with shared history and limits |
| 09 | Magentic planner | Plan, delegate, review progress, and synthesize |

Demos 04 and 07 require function or tool calling. Demo 09 additionally requires
structured JSON responses for the native Magentic manager.

## Structure

```text
src/MAFAgentLab.Core/             UI-independent agents, workflows, and traces
  Demos/                        All nine Agent Framework scenarios
  Helpers/                      Configuration, streaming, catalog, and replay
src/MAFAgentLab/                  Interactive console application
  Console/                      Menus, provider selection, live status, and rendering
  appsettings.json              Non-secret local defaults
tests/MAFAgentLab.Tests/          Offline framework and configuration tests
```

## Validate

```powershell
dotnet build MAFAgentLab.slnx --configuration Release
dotnet test --solution MAFAgentLab.slnx --configuration Release
```

The tests use local scripted `IChatClient` implementations and do not make paid
model calls. Actual provider connectivity, tool behavior, and response quality
depend on the configured services and models.
