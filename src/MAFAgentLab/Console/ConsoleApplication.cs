using MAFAgentLab.Core.Helpers;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace MAFAgentLab.Console;

/// <summary>Runs the interactive console experience.</summary>
internal static class ConsoleApplication
{
    private const int RunTimeoutMinutes = 5;

    /// <summary>Starts the console menu or a non-interactive replay from command-line arguments.</summary>
    public static async Task RunAsync(IConfiguration configuration, string[] args)
    {
        if (TryGetReplayId(args, out string? replayId))
        {
            DemoDefinition? replayDemo = DemoCatalog.Find(replayId!);
            if (replayDemo is null)
                throw new ArgumentException($"Unknown demo '{replayId}'. Expected an ID from 01 to 09.");
            await ExecuteAsync(replayDemo, configuration, prompt: null, followUp: null, replay: true);
            return;
        }

        while (true)
        {
            RenderHeader(configuration);
            MainAction action = AnsiConsole.Prompt(
                new SelectionPrompt<MainAction>()
                    .Title("[aqua]What would you like to explore?[/]")
                    .PageSize(8)
                    .UseConverter(DisplayName)
                    .AddChoices(MainAction.Live, MainAction.Replay, MainAction.Configuration, MainAction.Exit));

            if (action == MainAction.Exit)
                return;
            if (action == MainAction.Configuration)
            {
                RenderConfiguration(configuration);
                WaitForReturn();
                continue;
            }

            DemoDefinition demo = ChooseDemo();
            bool replay = action == MainAction.Replay;
            IConfiguration runConfiguration = replay || demo.Id == "03"
                ? configuration
                : ChooseProvider(configuration);

            if (!replay)
            {
                IReadOnlyList<string> missing = demo.GetMissingRequirements(runConfiguration);
                if (missing.Count > 0)
                {
                    RenderMissingConfiguration(missing);
                    WaitForReturn();
                    continue;
                }
            }

            string? prompt = replay ? null : PromptForRequest(demo);
            string? followUp = replay || demo.Id != "02"
                ? null
                : AnsiConsole.Ask("Follow-up", DemoCatalog.ConversationFollowUp);

            await ExecuteAsync(demo, runConfiguration, prompt, followUp, replay);
            WaitForReturn();
        }
    }

    /// <summary>Executes a live or replayed demo and renders its trace.</summary>
    private static async Task ExecuteAsync(
        DemoDefinition demo,
        IConfiguration configuration,
        string? prompt,
        string? followUp,
        bool replay)
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(RunTimeoutMinutes));
        ConsoleCancelEventHandler cancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        System.Console.CancelKeyPress += cancel;
        RunTrace trace = new();
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots12)
                .SpinnerStyle(Style.Parse("aqua"))
                .StartAsync($"[grey]Starting {Markup.Escape(demo.Title)}...[/]", async status =>
                {
                    Task run = ConsoleDemoRunner.RunAsync(
                        demo, configuration, trace, prompt, followUp, replay, cancellation.Token);

                    while (!run.IsCompleted)
                    {
                        RunSnapshot snapshot = trace.Snapshot();
                        AgentSnapshot? active = snapshot.Agents.LastOrDefault(agent => agent.State == "running");
                        status.Status = active is null
                            ? $"[grey]responses: {snapshot.Responses.Count} | events: {snapshot.Events.Count}[/]"
                            : $"[aqua]{Markup.Escape(active.Name)}[/] is working | responses: {snapshot.Responses.Count}";
                        await Task.WhenAny(run, Task.Delay(120, cancellation.Token));
                    }

                    await run;
                });
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Run cancelled.[/]");
        }
        catch (Exception error)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(error.Message)}[/]");
        }
        finally
        {
            System.Console.CancelKeyPress -= cancel;
        }

        RenderRun(demo, trace.Snapshot());
    }

    /// <summary>Renders the application banner and active provider.</summary>
    private static void RenderHeader(IConfiguration configuration)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Agent Lab")
                .Centered()
                .Color(Color.Aqua));
        AnsiConsole.Write(new Rule("[aqua]Microsoft Agent Framework - Console Demo[/]").Centered());
        AnsiConsole.MarkupLine(
            $"[grey]Active provider:[/] [bold]{Markup.Escape(ConfigurationHelper.GetAgentProvider(configuration))}[/]  " +
            $"[grey]Priority: Azure OpenAI -> Ollama -> OpenAI[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Prompts the user to select a demo.</summary>
    private static DemoDefinition ChooseDemo() =>
        AnsiConsole.Prompt(
            new SelectionPrompt<DemoDefinition>()
                .Title("[aqua]Select a demo[/]")
                .PageSize(12)
                .UseConverter(demo => $"{demo.Id}  {demo.Title} [grey]| {demo.Pattern}[/]")
                .AddChoices(DemoCatalog.All));

    /// <summary>Prompts the user to choose the provider for the next run.</summary>
    private static IConfiguration ChooseProvider(IConfiguration configuration)
    {
        string selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[aqua]Select a provider for this run[/]")
                .UseConverter(provider => provider switch
                {
                    "Auto" => "Auto  [grey]Azure OpenAI -> Ollama -> OpenAI[/]",
                    _ => provider
                })
                .AddChoices("Auto", "AzureOpenAI", "Ollama", "OpenAI"));

        return new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agent:Provider"] = selected })
            .Build();
    }

    /// <summary>Prompts the user for a demo request.</summary>
    private static string PromptForRequest(DemoDefinition demo) =>
        AnsiConsole.Prompt(
            new TextPrompt<string>("[aqua]Prompt[/]")
                .DefaultValue(demo.ExamplePrompt)
                .ShowDefaultValue());

    /// <summary>Renders the readiness of all supported external services.</summary>
    private static void RenderConfiguration(IConfiguration configuration)
    {
        Table table = new Table().RoundedBorder().BorderColor(Color.Grey)
            .Title("[aqua]Configuration[/]")
            .AddColumn("Service")
            .AddColumn("Status")
            .AddColumn("Missing values");

        AddConfigurationRow(table, "Azure OpenAI", ConfigurationHelper.GetMissingAzureOpenAISettings(configuration));
        AddConfigurationRow(table, "Ollama", ConfigurationHelper.GetMissingOllamaSettings(configuration));
        AddConfigurationRow(table, "OpenAI", ConfigurationHelper.GetMissingOpenAISettings(configuration));
        AddConfigurationRow(table, "OpenWeatherMap (Demo 04)", ConfigurationHelper.GetMissingWeatherSettings(configuration));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            "\n[grey]Secrets are loaded from Visual Studio User Secrets and then from environment variables. " +
            "Values and API keys are never displayed in this table.[/]");
        AnsiConsole.MarkupLine(
            "[grey]Example:[/] [aqua]dotnet user-secrets set --project src/MAFAgentLab \"Ollama:Deployment\" \"llama3.2\"[/]");
    }

    /// <summary>Adds one service's readiness and missing keys to a table.</summary>
    private static void AddConfigurationRow(Table table, string name, IReadOnlyList<string> missing)
    {
        table.AddRow(
            name,
            missing.Count == 0 ? "[green]ready[/]" : "[yellow]incomplete[/]",
            missing.Count == 0 ? "[grey]-[/]" : Markup.Escape(string.Join(", ", missing)));
    }

    /// <summary>Renders an actionable panel for missing demo configuration.</summary>
    private static void RenderMissingConfiguration(IReadOnlyList<string> missing)
    {
        Panel panel = new Panel(
            $"[yellow]{Markup.Escape(string.Join(Environment.NewLine, missing))}[/]\n\n" +
            "[grey]Open the configuration overview to see the required keys. " +
            "Store the values with Visual Studio User Secrets.[/]")
        {
            Header = new PanelHeader(" Configuration incomplete "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
        AnsiConsole.Write(panel);
    }

    /// <summary>Renders the summary, responses, agents, and trace for a completed run.</summary>
    private static void RenderRun(DemoDefinition demo, RunSnapshot snapshot)
    {
        AnsiConsole.Write(new Rule($"[aqua]{demo.Id} | {Markup.Escape(demo.Title)}[/]").Centered());

        Table overview = new Table().Border(TableBorder.None).AddColumn("").AddColumn("");
        overview.AddRow("[grey]Status[/]", snapshot.State == "completed" ? "[green]completed[/]" : $"[yellow]{Markup.Escape(snapshot.State)}[/]");
        overview.AddRow("[grey]Duration[/]", $"{snapshot.Elapsed.TotalSeconds:F1} s");
        overview.AddRow("[grey]Agents[/]", snapshot.Agents.Count.ToString());
        overview.AddRow("[grey]Responses[/]", snapshot.Responses.Count.ToString());
        AnsiConsole.Write(overview);

        if (snapshot.Agents.Count > 0)
        {
            Table agents = new Table().RoundedBorder().BorderColor(Color.Grey)
                .AddColumn("Participant").AddColumn("Status").AddColumn("Turns").AddColumn("Time");
            foreach (AgentSnapshot agent in snapshot.Agents)
                agents.AddRow(
                    Markup.Escape(agent.Name),
                    Markup.Escape(agent.State),
                    agent.Turns.ToString(),
                    $"{agent.Duration.TotalSeconds:F1} s");
            AnsiConsole.Write(agents);
        }

        foreach (TraceResponse response in snapshot.Responses)
        {
            Panel panel = new Panel(Markup.Escape(response.Text))
            {
                Header = new PanelHeader(
                    $" {(response.IsFinal ? "Final" : "Response")} | {Markup.Escape(response.Actor ?? "Workflow")} "),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(response.IsFinal ? Color.Green : response.IsTool ? Color.Yellow : Color.Aqua),
                Expand = true
            };
            AnsiConsole.Write(panel);
        }

        if (snapshot.Events.Count > 0)
        {
            Table events = new Table().RoundedBorder().BorderColor(Color.Grey).Title("[grey]Trace[/]")
                .AddColumn("Time").AddColumn("Actor").AddColumn("Event");
            foreach (TraceEvent entry in snapshot.Events.TakeLast(14))
                events.AddRow(
                    $"{entry.Elapsed.TotalSeconds:F1}s",
                    Markup.Escape(entry.Actor),
                    Markup.Escape(entry.Detail));
            AnsiConsole.Write(events);
        }
    }

    /// <summary>Waits for the user to return to the main menu.</summary>
    private static void WaitForReturn()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Prompt(new TextPrompt<string>("[grey]Press Enter to return to the main menu[/]").DefaultValue("").HideDefaultValue());
    }

    /// <summary>Converts a menu action to user-facing text.</summary>
    private static string DisplayName(MainAction action) => action switch
    {
        MainAction.Live => "Start a live demo",
        MainAction.Replay => "Start a guided replay without API calls",
        MainAction.Configuration => "Show provider configuration",
        MainAction.Exit => "Exit",
        _ => action.ToString()
    };

    /// <summary>Tries to read a replay demo identifier from command-line arguments.</summary>
    private static bool TryGetReplayId(string[] args, out string? id)
    {
        int replayIndex = Array.IndexOf(args, "--replay");
        if (replayIndex >= 0 && replayIndex + 1 < args.Length)
        {
            id = args[replayIndex + 1];
            return true;
        }
        id = null;
        return false;
    }

}
