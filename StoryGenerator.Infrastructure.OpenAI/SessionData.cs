namespace StoryGenerator.Core.Models
{
    public class SessionData
    {
        public string Id { get; set; } = string.Empty;
        public List<ChatMessage> Messages { get; set; } = new();
        public Dictionary<string, object> State { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}