

namespace StoryGenerator.AI.Services
{
    internal class ChatCompletionOptions : OpenAI.Chat.ChatCompletionOptions
    {
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
    }
}