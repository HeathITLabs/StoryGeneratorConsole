using StoryGenerator.Core.Models;

namespace StoryGenerator.AI.Services
{
    public interface IOpenAIService
    {
        Task<string> GenerateTextAsync(
            List<ChatMessage> messages,
            string model = "gemma3",
            int maxTokens = 2000,
            double temperature = 0.7);

        Task<string> GenerateCompletionAsync(
            string prompt,
            string model = "gemma3",
            int maxTokens = 2000,
            double temperature = 0.7);

        Task<T> GenerateStructuredOutputAsync<T>(
            List<ChatMessage> messages,
            string model = "gemma3")
            where T : class;

        Task<string> GenerateWithHistoryAsync(
            List<ChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gemma3",
            int maxTokens = 2000);

        Task<T> GenerateStructuredWithHistoryAsync<T>(
            List<ChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gemma3")
            where T : class;
    }
}