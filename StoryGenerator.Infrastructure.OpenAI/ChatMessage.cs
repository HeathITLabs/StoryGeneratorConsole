using System.ComponentModel.DataAnnotations;

namespace StoryGenerator.Core.Models
{
    public class ChatMessage
    {
        [Required]
        public string Role { get; set; } = "user";
        [Required]
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}