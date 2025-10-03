using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Models;
using StoryGeneratorConsole.StoryGenerator.Application;
using StoryGeneratorConsole.StoryGenerator.Application.Flows;
using StoryGenerator.Presentation.Console; // StoryGameService
using StoryGenerator.Presentation.Discord; // BotSessionRegistry, DiscordBotWorker

namespace StoryGenerator.Presentation.Discord
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // config
            builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                                 .AddEnvironmentVariables();

            // logging
            builder.Services.AddLogging(b =>
            {
                b.AddConsole();
                b.SetMinimumLevel(LogLevel.Information);
            });

            // Http + DI
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

            // Engine + game service
            builder.Services.AddSingleton<IFlowEngine, FlowEngine>();
            builder.Services.AddSingleton<IStoryGame, StoryGameService>();

            // Discord
            builder.Services.AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.DirectMessages
            }));
            builder.Services.AddSingleton<InteractionService>();
            builder.Services.AddSingleton<BotSessionRegistry>();
            builder.Services.AddHostedService<DiscordBotWorker>();

            var app = builder.Build();
            await app.RunAsync();
        }
    }
}
