using System;
using System.Collections.Concurrent;

namespace StoryGenerator.Presentation.Discord
{
    public sealed class BotSessionRegistry
    {
        private sealed record Session(string SessionId, string? Style, string? Theme);

        private readonly ConcurrentDictionary<string, Session> _sessions = new();

        private static string Key(ulong? guildId, ulong channelId, ulong userId)
            => $"{guildId?.ToString() ?? "dm"}:{channelId}:{userId}";

        public string GetOrCreateSessionId(ulong? guildId, ulong channelId, ulong userId)
        {
            var key = Key(guildId, channelId, userId);
            return _sessions.GetOrAdd(key, _ => new Session(Guid.NewGuid().ToString("N"), null, null)).SessionId;
        }

        public void SetStyleTheme(ulong? guildId, ulong channelId, ulong userId, string? style, string? theme)
        {
            var key = Key(guildId, channelId, userId);
            _sessions.AddOrUpdate(key,
                _ => new Session(Guid.NewGuid().ToString("N"), style, theme),
                (_, s) => s with { Style = style, Theme = theme });
        }

        public (string? style, string? theme) GetStyleTheme(ulong? guildId, ulong channelId, ulong userId)
        {
            _sessions.TryGetValue(Key(guildId, channelId, userId), out var s);
            return (s?.Style, s?.Theme);
        }
    }
}