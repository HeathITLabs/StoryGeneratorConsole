using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
//using Spectre.Console.ImageSharp;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Models;
using StoryGeneratorConsole.StoryGenerator.Application;
using StoryGeneratorConsole.StoryGenerator.Application.Flows;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json").AddEnvironmentVariables();

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

await ConsoleUi.RunAsync(app.Services, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("App"));