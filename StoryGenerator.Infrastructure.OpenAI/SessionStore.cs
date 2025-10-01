using System.Collections.Concurrent;

namespace StoryGenerator.Core.Models
{
    public class SessionState
    {
        public string SessionId { get; init; } = string.Empty;
        public List<ChatMessage> Messages { get; } = new();
        public List<string> StoryParts { get; } = new();
        public List<string> Options { get; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public double Progress { get; set; }
        public string Rating { get; set; } = string.Empty;
        public List<string> ImagePaths { get; } = new();
    }

    public interface ISessionStore
    {
        SessionState GetOrCreate(string sessionId);
        void Clear(string sessionId);
        void AppendMessage(string sessionId, ChatMessage message);
        void Update(string sessionId, Action<SessionState> updater);
    }

    public class InMemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<string, SessionState> _sessions = new();

        public SessionState GetOrCreate(string sessionId)
            => _sessions.GetOrAdd(sessionId, id => new SessionState { SessionId = id });

        public void Clear(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        public void AppendMessage(string sessionId, ChatMessage message)
        {
            var state = GetOrCreate(sessionId);
            lock (state)
            {
                state.Messages.Add(message);
            }
        }

        public void Update(string sessionId, Action<SessionState> updater)
        {
            var state = GetOrCreate(sessionId);
            lock (state)
            {
                updater(state);
            }
        }
    }
}