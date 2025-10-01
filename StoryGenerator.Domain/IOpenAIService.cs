using StoryGenerator.Core.Models;

namespace StoryGenerator.AI.Services
{
    public interface IOpenAIService
    {
        Task<string> GenerateTextAsync(
            List<ChatMessage> messages,
            string model = "gpt-3.5-turbo",
            int maxTokens = 2000,
            double temperature = 0.7);

        Task<string> GenerateCompletionAsync(
            string prompt,
            string model = "gpt-3.5-turbo",
            int maxTokens = 2000,
            double temperature = 0.7);

        Task<T> GenerateStructuredOutputAsync<T>(
            List<ChatMessage> messages,
            string model = "gpt-3.5-turbo")
            where T : class;

        Task<string> GenerateWithHistoryAsync(
            List<ChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gpt-3.5-turbo",
            int maxTokens = 2000);

        Task<T> GenerateStructuredWithHistoryAsync<T>(
            List<ChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gpt-3.5-turbo")
            where T : class;
    }
}