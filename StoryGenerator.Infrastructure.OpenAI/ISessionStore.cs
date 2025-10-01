using StoryGenerator.Core.Models;

namespace StoryGenerator.Core.Services
{
    public interface ISessionStore
    {
        string CreateSession(string? sessionId = null);
        SessionData? GetSession(string sessionId);
        void UpdateSession(string sessionId, Action<SessionData> update);
        void DeleteSession(string sessionId);
        void ClearSession(string sessionId);
        
        // Message management
        void AddMessage(string sessionId, ChatMessage message);
        List<ChatMessage> GetMessages(string sessionId);
        
        // State management
        void SetState<T>(string sessionId, string key, T value);
        T? GetState<T>(string sessionId, string key);
        
        // Cleanup
        void CleanupExpiredSessions(TimeSpan maxAge);
        List<string> ListSessions();
    }
}