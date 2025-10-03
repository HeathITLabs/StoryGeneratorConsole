using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;

namespace StoryGenerator.Presentation.Discord
{
    public sealed class DiscordBotWorker : BackgroundService
    {
        private readonly ILogger<DiscordBotWorker> _logger;
        private readonly IConfiguration _config;
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;

        public DiscordBotWorker(
            ILogger<DiscordBotWorker> logger,
            IConfiguration config,
            DiscordSocketClient client,
            InteractionService interactions,
            IServiceProvider services)
        {
            _logger = logger;
            _config = config;
            _client = client;
            _interactions = interactions;
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Log += msg => { _logger.LogInformation("[Discord] {Msg}", msg.ToString()); return Task.CompletedTask; };
            _interactions.Log += msg => { _logger.LogInformation("[Discord.Interactions] {Msg}", msg.ToString()); return Task.CompletedTask; };

            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += OnInteractionAsync;

            var token = _config["Discord:Token"];
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Discord:Token not configured.");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1, stoppingToken);
        }

        private async Task OnReadyAsync()
        {
            // Register slash commands
            await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            var guildId = _config.GetValue<ulong?>("Discord:GuildId"); // optional: faster dev updates
            if (guildId.HasValue)
            {
                await _interactions.RegisterCommandsToGuildAsync(guildId.Value);
                _logger.LogInformation("Registered commands to guild {Guild}", guildId.Value);
            }
            else
            {
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Registered commands globally");
            }
        }

        private async Task OnInteractionAsync(SocketInteraction arg)
        {
            var ctx = new SocketInteractionContext(_client, arg);
            await _interactions.ExecuteCommandAsync(ctx, _services);
        }
    }
}