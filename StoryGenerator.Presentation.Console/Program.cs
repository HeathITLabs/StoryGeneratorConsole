using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Models;
using StoryGeneratorConsole.StoryGenerator.Application;
using StoryGeneratorConsole.StoryGenerator.Application.Flows;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables();

// Logging
builder.Services.AddLogging(b =>
{
    b.AddConsole();
    b.SetMinimumLevel(LogLevel.Information);
});

// Http + DI registrations
builder.Services.AddHttpClient();

// Core services
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
builder.Services.AddSingleton<IImageGenerationService, ComfyUIImageService>();

// Flows
builder.Services.AddTransient<DescriptionFlow>();
builder.Services.AddTransient<BeginStoryFlow>();
builder.Services.AddTransient<ContinueStoryFlow>();
builder.Services.AddTransient<ImageGenerationFlow>();

// Engine
builder.Services.AddSingleton<IFlowEngine, FlowEngine>();

var app = builder.Build();

await RunAsync(app.Services, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("App"));

static async Task RunAsync(IServiceProvider sp, ILogger logger)
{
    AnsiConsole.Clear();
    ShowWelcome();

    var engine = sp.GetRequiredService<IFlowEngine>();
    var sessions = sp.GetRequiredService<ISessionStore>();

    var sessionId = Guid.NewGuid().ToString("N");
    var satisfied = false;

    // Description phase
    string? lastUser = null;
    while (!satisfied)
    {
        var input = new DescriptionFlowInput
        {
            UserInput = lastUser,
            SessionId = sessionId,
            ClearSession = false
        };

        DescriptionFlowOutput? desc = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Thinking...", async ctx =>
            {
                desc = await engine.ExecuteAsync<DescriptionFlowInput, DescriptionFlowOutput>("Description", input);
            });

        AnsiConsole.Write(new Panel(desc!.StoryPremise).Header("Story Premise").Border(BoxBorder.Double));
        AnsiConsole.MarkupLine($"[bold]Next:[/]: {desc!.NextQuestion}");

        string? selected = null;
        if (desc.PremiseOptions.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded).Title("Options");
            table.AddColumn("No.");
            table.AddColumn("Option");
            int i = 1;
            foreach (var o in desc.PremiseOptions)
                table.AddRow(i++.ToString(), o);
            AnsiConsole.Write(table);

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose an option or [green]Type your own[/] (press Esc to type)")
                    .AddChoices(desc.PremiseOptions));

            selected = choice;
        }
        else
        {
            selected = AnsiConsole.Ask<string>("Answer the question (or type 'start' to begin):");
        }

        if (string.Equals(selected, "start", StringComparison.OrdinalIgnoreCase))
        {
            satisfied = true;
        }
        else
        {
            lastUser = AnsiConsole.Confirm("Use the selected option? (No to type your own)")
                ? selected
                : AnsiConsole.Ask<string>("Your answer:");
        }
    }

    // Begin story
    BeginStoryFlowOutput begin = default!;
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Starting story...", async _ =>
        {
            begin = await engine.ExecuteAsync<BeginStoryFlowInput, BeginStoryFlowOutput>(
                "Begin",
                new BeginStoryFlowInput { UserInput = "Begin the story.", SessionId = sessionId });
        });

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

            ContinueStoryFlowOutput cont = default!;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Continuing story...", async _ =>
                {
                    cont = await engine.ExecuteAsync<ContinueStoryFlowInput, ContinueStoryFlowOutput>(
                        "Continue",
                        new ContinueStoryFlowInput { UserInput = choice, SessionId = sessionId });
                });

            RenderStory(cont.StoryParts, cont.PrimaryObjective, cont.Progress, cont.Rating);

            // Image generation for the latest part
            var latest = cont.StoryParts.LastOrDefault();
            if (!string.IsNullOrWhiteSpace(latest))
            {
                ImageGenerationOutput imageOut = default!;
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Earth)
                    .StartAsync("Generating image...", async _ =>
                    {
                        imageOut = await engine.ExecuteAsync<ImageGenerationInput, ImageGenerationOutput>(
                            "Image",
                            new ImageGenerationInput { Story = latest!, SessionId = sessionId });
                    });

                AnsiConsole.MarkupLine($"[green]Image saved:[/] {imageOut.FilePath}");
            }

            begin = new BeginStoryFlowOutput
            {
                StoryParts = cont.StoryParts,
                Options = cont.Options,
                PrimaryObjective = cont.PrimaryObjective,
                Progress = cont.Progress
            };

            if (cont.Progress >= 1.0 - 1e-6)
            {
                AnsiConsole.MarkupLine("[bold green]The primary objective has been achieved![/]");
                break;
            }
        }
        else
        {
            // No options; ask user directly
            var free = AnsiConsole.Ask<string>("Describe the next action:");
            ContinueStoryFlowOutput cont = default!;
            await AnsiConsole.Status().StartAsync("Continuing...", async _ =>
            {
                cont = await engine.ExecuteAsync<ContinueStoryFlowInput, ContinueStoryFlowOutput>(
                    "Continue",
                    new ContinueStoryFlowInput { UserInput = free, SessionId = sessionId });
            });
            RenderStory(cont.StoryParts, cont.PrimaryObjective, cont.Progress, cont.Rating);

            if (cont.Progress >= 1.0 - 1e-6)
            {
                AnsiConsole.MarkupLine("[bold green]The primary objective has been achieved![/]");
                break;
            }
        }

        if (!AnsiConsole.Confirm("Continue?"))
            break;
    }

    AnsiConsole.MarkupLine("[dim]Thanks for playing![/]");
}

static void ShowWelcome()
{
    var rule = new Rule("[yellow]Interactive Story Generator[/]") { Justification = Justify.Center };
    AnsiConsole.Write(rule);
    AnsiConsole.Write(
@"[cyan]
Its  Dungeon Time
[/]");
}

static void RenderStory(List<string> parts, string objective, double progress, string? rating = null)
{
    if (!string.IsNullOrWhiteSpace(rating))
        AnsiConsole.MarkupLine($"Choice rating: [bold]{rating}[/]");

    foreach (var part in parts)
    {
        AnsiConsole.Write(new Panel(part).Header("Story").Border(BoxBorder.Rounded));
    }

    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Primary Objective");
    table.AddColumn("Progress");
    table.AddRow(objective, $"{progress:P0}");
    AnsiConsole.Write(table);
}