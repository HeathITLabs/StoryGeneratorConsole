using StoryGenerator.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace StoryGenerator.Core.Services
{
    public class SessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<string, SessionData> _sessions = new();

        public string CreateSession(string? sessionId = null)
        {
            var id = sessionId ?? Guid.NewGuid().ToString();
            var session = new SessionData
            {
                Id = id,
                Messages = new List<ChatMessage>(),
                State = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _sessions.TryAdd(id, session);
            return id;
        }

        public SessionData? GetSession(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public void UpdateSession(string sessionId, Action<SessionData> update)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                update(session);
                session.UpdatedAt = DateTime.UtcNow;
            }
        }

        public void DeleteSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        public void ClearSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Messages.Clear();
                session.State.Clear();
                session.UpdatedAt = DateTime.UtcNow;
            }
        }

        public void AddMessage(string sessionId, ChatMessage message)
        {
            UpdateSession(sessionId, session => session.Messages.Add(message));
        }

        public List<ChatMessage> GetMessages(string sessionId)
        {
            var session = GetSession(sessionId);
            return session?.Messages ?? new List<ChatMessage>();
        }

        public void SetState<T>(string sessionId, string key, T value)
        {
            UpdateSession(sessionId, session => 
            {
                session.State[key] = value!;
            });
        }

        public T? GetState<T>(string sessionId, string key)
        {
            var session = GetSession(sessionId);
            if (session?.State.TryGetValue(key, out var value) == true)
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                if (value is T directValue)
                {
                    return directValue;
                }
                // Try to convert
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        public void CleanupExpiredSessions(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var expiredSessions = _sessions
                .Where(kvp => kvp.Value.UpdatedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.TryRemove(sessionId, out _);
            }
        }

        public List<string> ListSessions()
        {
            return _sessions.Keys.ToList();
        }
    }
}