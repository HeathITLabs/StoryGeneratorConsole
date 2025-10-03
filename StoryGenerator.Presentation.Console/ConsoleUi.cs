using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using StoryGenerator.Core.Models;
using StoryGeneratorConsole.StoryGenerator.Application;
using StoryGeneratorConsole.StoryGenerator.Application.Flows;
using System.Linq;

internal static class ConsoleUi
{
    internal static async Task RunAsync(IServiceProvider sp, ILogger logger)
    {
        AnsiConsole.Clear();
        ShowWelcome();

        var engine = sp.GetRequiredService<IFlowEngine>();

        // Ask once per session: preferred image style and optional theme
        var (imageStyle, imageTheme) = PromptImagePreferences();

        var sessionId = Guid.NewGuid().ToString("N");

        // Description phase
        await RunDescriptionPhaseAsync(engine, sessionId);

        // Begin story
        var begin = await ExecuteWithStatusAsync<BeginStoryFlowInput, BeginStoryFlowOutput>(
            engine,
            FlowNames.Begin,
            new BeginStoryFlowInput { UserInput = "Begin the story.", SessionId = sessionId },
            "Starting story...",
            Spinner.Known.Dots);

        RenderStory(begin.StoryParts, begin.PrimaryObjective, begin.Progress);

        // Main loop
        while (true)
        {
            if (begin.Options.Count > 0)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What should happen next?")
                        .AddChoices(begin.Options));

                var cont = await ExecuteWithStatusAsync<ContinueStoryFlowInput, ContinueStoryFlowOutput>(
                    engine,
                    FlowNames.Continue,
                    new ContinueStoryFlowInput { UserInput = choice, SessionId = sessionId },
                    "Continuing story...",
                    Spinner.Known.Dots);

                RenderStory(cont.StoryParts, cont.PrimaryObjective, cont.Progress, cont.Rating);

                await GenerateAndRenderImageAsync(engine, sessionId, cont.StoryParts.LastOrDefault(), imageStyle, imageTheme);

                begin = ToBegin(cont);

                if (IsComplete(cont.Progress))
                {
                    AnsiConsole.MarkupLine("[bold green]The primary objective has been achieved![/]");
                    break;
                }
            }
            else
            {
                // No options; ask user directly
                var free = AnsiConsole.Ask<string>("Describe the next action:");
                var cont = await ExecuteWithStatusAsync<ContinueStoryFlowInput, ContinueStoryFlowOutput>(
                    engine,
                    FlowNames.Continue,
                    new ContinueStoryFlowInput { UserInput = free, SessionId = sessionId },
                    "Continuing...",
                    Spinner.Known.Dots);

                RenderStory(cont.StoryParts, cont.PrimaryObjective, cont.Progress, cont.Rating);

                if (IsComplete(cont.Progress))
                {
                    AnsiConsole.MarkupLine("[bold green]The primary objective has been achieved![/]");
                    break;
                }

                begin = ToBegin(cont);
            }

            if (!AnsiConsole.Confirm("Continue?"))
                break;
        }

        AnsiConsole.MarkupLine("[dim]Thanks for playing![/]");
    }

    private static (string Style, string? Theme) PromptImagePreferences()
    {
        var style = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select an illustration [green]style[/]:")
                .AddChoices(new[]
                {
                    "Graphic Novel", "Watercolor", "Pixel Art", "Anime",
                    "Noir", "Oil Painting", "Isometric", "Low Poly",
                    "Realistic", "Cyberpunk", "ASCII Art", "Fantasy"
                }));

        var theme = AnsiConsole.Ask<string>(
            "Optional [grey]theme/mood keywords[/] (e.g., 'dark fantasy, steampunk') [dim](leave blank to skip)[/]:",
            "");

        return (style, string.IsNullOrWhiteSpace(theme) ? null : theme);
    }

    private static async Task RunDescriptionPhaseAsync(IFlowEngine engine, string sessionId)
    {
        string? lastUser = null;

        while (true)
        {
            var desc = await ExecuteWithStatusAsync<DescriptionFlowInput, DescriptionFlowOutput>(
                engine,
                FlowNames.Description,
                new DescriptionFlowInput
                {
                    UserInput = lastUser,
                    SessionId = sessionId,
                    ClearSession = false
                },
                "Thinking...",
                Spinner.Known.Dots);

            AnsiConsole.Write(new Panel(desc.StoryPremise).Header("Story Premise").Border(BoxBorder.Double));
            AnsiConsole.MarkupLine($"[bold]Next:[/]: {desc.NextQuestion}");

            var answer = PromptPremiseAnswer(desc);

            if (string.Equals(answer, "start", StringComparison.OrdinalIgnoreCase))
                break;

            lastUser = answer;
        }
    }

    private static string PromptPremiseAnswer(DescriptionFlowOutput desc)
    {
        string? selected = null;

        if (desc.PremiseOptions.Count > 0)
        {
            RenderOptionsTable(desc.PremiseOptions);

            selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose an option or [green]Type your own[/] (press Esc to type)")
                    .AddChoices(desc.PremiseOptions));
        }
        else
        {
            selected = AnsiConsole.Ask<string>("Answer the question (or type 'start' to begin):");
        }

        if (!string.Equals(selected, "start", StringComparison.OrdinalIgnoreCase))
        {
            selected = AnsiConsole.Confirm("Use the selected option? (No to type your own)")
                ? selected
                : AnsiConsole.Ask<string>("Your answer:");
        }

        return selected!;
    }

    private static void RenderOptionsTable(List<string> options)
    {
        var table = new Table().Border(TableBorder.Rounded).Title("Options");
        table.AddColumn("No.");
        table.AddColumn("Option");
        int i = 1;
        foreach (var o in options)
            table.AddRow(i++.ToString(), o);
        AnsiConsole.Write(table);
    }

    private static async Task GenerateAndRenderImageAsync(
        IFlowEngine engine,
        string sessionId,
        string? latestStoryPart,
        string style,
        string? theme)
    {
        if (string.IsNullOrWhiteSpace(latestStoryPart))
            return;

        var imageOut = await ExecuteWithStatusAsync<ImageGenerationInput, ImageGenerationOutput>(
            engine,
            FlowNames.Image,
            new ImageGenerationInput
            {
                Story = latestStoryPart!,
                SessionId = sessionId,
                Style = style,
                Theme = theme
            },
            "Generating image...",
            Spinner.Known.Earth);

        AnsiConsole.MarkupLine($"[green]Image saved:[/] {imageOut.FilePath}");
        RenderImageIfExists(imageOut.FilePath);
    }

    private static void RenderImageIfExists(string path)
    {
        if (!System.IO.File.Exists(path))
            return;

        var img = new CanvasImage(path)
        {
            MaxWidth = Math.Max(10, Math.Min(AnsiConsole.Profile.Width - 4, 100))
        };
        AnsiConsole.Write(img);
    }

    private static async Task<TOut> ExecuteWithStatusAsync<TIn, TOut>(
        IFlowEngine engine,
        string flowName,
        TIn input,
        string statusMessage,
        Spinner? spinner)
    {
        TOut? result = default!;
        await AnsiConsole.Status()
            .Spinner(spinner ?? Spinner.Known.Dots)
            .StartAsync(statusMessage, async _ =>
            {
                result = await engine.ExecuteAsync<TIn, TOut>(flowName, input);
            });
        return result!;
    }

    private static BeginStoryFlowOutput ToBegin(ContinueStoryFlowOutput cont) => new()
    {
        StoryParts = cont.StoryParts,
        Options = cont.Options,
        PrimaryObjective = cont.PrimaryObjective,
        Progress = cont.Progress
    };

    private static bool IsComplete(double progress) => progress >= 1.0 - 1e-6;

    private static void ShowWelcome()
    {
        var rule = new Rule("[yellow]Interactive Story Generator[/]") { Justification = Justify.Center };
        AnsiConsole.Write(rule);
        AnsiConsole.Write(@"[red]Its Dungeon Time[/]");
    }

    private static void RenderStory(List<string> parts, string objective, double progress, string? rating = null)
    {
        if (!string.IsNullOrWhiteSpace(rating))
            AnsiConsole.MarkupLine($"Choice rating: [bold]{rating}[/]");

        foreach (var part in parts)
            AnsiConsole.Write(new Panel(part).Header("Story").Border(BoxBorder.Rounded));

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Primary Objective");
        table.AddColumn("Progress");
        table.AddRow(objective, $"{progress:P0}");
        AnsiConsole.Write(table);
    }
}