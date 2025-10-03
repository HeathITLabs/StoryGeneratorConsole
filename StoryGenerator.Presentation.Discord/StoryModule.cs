using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Interactions;
using StoryGeneratorConsole.StoryGenerator.Application;
using StoryGenerator.Core.Models;
using StoryGenerator.Presentation.Discord;
using ContinueOut = StoryGeneratorConsole.StoryGenerator.Application.Flows.ContinueStoryFlowOutput;

namespace StoryGenerator.Presentation.Discord
{
    public sealed class StoryModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IStoryGame _game;
        private readonly BotSessionRegistry _sessions;

        public StoryModule(IStoryGame game, BotSessionRegistry sessions)
        {
            _game = game;
            _sessions = sessions;
        }

        [SlashCommand("play", "Start or resume premise building; asks the next question")]
        public async Task PlayAsync(
            [Summary("style", "Optional illustration style, e.g., 'Graphic Novel'")] string? style = null,
            [Summary("theme", "Optional theme keywords, e.g., 'dark fantasy'")] string? theme = null)
        {
            await DeferAsync(ephemeral: false);
            _sessions.SetStyleTheme(Context.Guild?.Id, Context.Channel.Id, Context.User.Id, style, theme);
            var sessionId = _sessions.GetOrCreateSessionId(Context.Guild?.Id, Context.Channel.Id, Context.User.Id);

            // null means "continue asking" with no new answer yet
            var desc = await _game.GetPremiseAsync(sessionId, null);

            var components = BuildOptionsComponents(desc.PremiseOptions);
            await FollowupAsync(embed: BuildPremiseEmbed(desc), components: components);
        }

        [SlashCommand("answer", "Answer the current premise question")]
        public async Task AnswerAsync([Summary("text", "Your answer or 'start' to begin")] string text)
        {
            await DeferAsync(ephemeral: false);
            var sessionId = _sessions.GetOrCreateSessionId(Context.Guild?.Id, Context.Channel.Id, Context.User.Id);
            var desc = await _game.GetPremiseAsync(sessionId, text);

            if (string.Equals(text, "start", System.StringComparison.OrdinalIgnoreCase))
            {
                var begin = await _game.BeginAsync(sessionId);
                await SendStoryAsync(begin.StoryParts, begin.PrimaryObjective, begin.Progress, begin.Options);
                return;
            }

            var components = BuildOptionsComponents(desc.PremiseOptions);
            await FollowupAsync(embed: BuildPremiseEmbed(desc), components: components);
        }

        [SlashCommand("begin", "Begin the story now")]
        public async Task BeginAsync()
        {
            await DeferAsync(ephemeral: false);
            var sessionId = _sessions.GetOrCreateSessionId(Context.Guild?.Id, Context.Channel.Id, Context.User.Id);
            var begin = await _game.BeginAsync(sessionId);
            await SendStoryAsync(begin.StoryParts, begin.PrimaryObjective, begin.Progress, begin.Options);
        }

        [SlashCommand("choose", "Choose what happens next (paste option text or type your own)")]
        public async Task ChooseAsync([Summary("choice", "Your choice")] string choice)
        {
            await DeferAsync(ephemeral: false);
            var sessionId = _sessions.GetOrCreateSessionId(Context.Guild?.Id, Context.Channel.Id, Context.User.Id);
            ContinueOut cont = await _game.ContinueAsync(sessionId, choice);

            await SendStoryAsync(cont.StoryParts, cont.PrimaryObjective, cont.Progress, cont.Options, cont.Rating);
        }

        [SlashCommand("image", "Generate an illustration for the most recent story part")]
        public async Task ImageAsync()
        {
            await DeferAsync(ephemeral: false);
            var sessionId = _sessions.GetOrCreateSessionId(Context.Guild?.Id, Context.Channel.Id, Context.User.Id);
            var tuple = _sessions.GetStyleTheme(Context.Guild?.Id, Context.Channel.Id, Context.User.Id);
            var style = tuple.style; var theme = tuple.theme;

            // Quick re-use: continue with a no-op choice to get last story part if needed,
            // but typically you should store the last part on each continue/begin.
            // For simplicity, ask the model to not change; better approach: track last text in your registry.
            var resumed = await _game.ContinueAsync(sessionId, ""); // may advance unexpectedly in some flows
            var latest = resumed.StoryParts.LastOrDefault();
            if (string.IsNullOrWhiteSpace(latest))
            {
                await FollowupAsync("No story content found yet. Use /begin or /choose first.");
                return;
            }

            var img = await _game.GenerateImageAsync(sessionId, latest!, style, theme);
            await FollowupWithFileAsync(img.FilePath, text: "Here is your illustration.");
        }

        private async Task SendStoryAsync(
            List<string> parts,
            string primaryObjective,
            double progress,
            List<string> options,
            string? rating = null)
        {
            // Send story parts as embeds
            int idx = 1;
            foreach (var p in parts)
            {
                var eb = new EmbedBuilder()
                    .WithTitle($"Story Part {idx++}")
                    .WithDescription(p)
                    .WithColor(Color.Blue);
                if (!string.IsNullOrWhiteSpace(rating))
                    eb.AddField("Rating", rating, inline: true);

                eb.AddField("Primary Objective", primaryObjective, inline: false)
                  .AddField("Progress", $"{progress:P0}", inline: true);

                await FollowupAsync(embed: eb.Build());
            }

            if (options?.Count > 0)
            {
                var components = BuildOptionsComponents(options);
                await FollowupAsync(text: "What should happen next? Use /choose or tap a button:", components: components);
            }
        }

        private static MessageComponent BuildOptionsComponents(List<string> options)
        {
            if (options is null || options.Count == 0)
                return new ComponentBuilder().Build();

            var builder = new ComponentBuilder();
            // Discord allows up to 5 buttons per row; slice accordingly
            const int perRow = 5;
            for (int i = 0; i < options.Count; i += perRow)
            {
                var row = options.Skip(i).Take(perRow).ToList();
                foreach (var o in row)
                {
                    // Button executes a /choose command suggestion via custom id; you may also handle via component interactions directly
                    builder.WithButton(o, customId: $"choose::{o}", style: ButtonStyle.Secondary);
                }
            }
            return builder.Build();
        }

        private static Embed BuildPremiseEmbed(DescriptionFlowOutput desc)
            => new EmbedBuilder()
                .WithTitle("Story Premise")
                .WithDescription(desc.StoryPremise)
                .WithColor(Color.Purple)
                .AddField("Next", desc.NextQuestion)
                .Build();
    }
}