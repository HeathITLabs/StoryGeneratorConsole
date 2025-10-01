using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Polly;
using StoryGenerator.Core.Models;
using System.Text.Json;
using System.ClientModel;
using System.Threading;

// Add these aliases to avoid type name collisions
using AIChatMessage = OpenAI.Chat.ChatMessage;
using SystemChatMessage = OpenAI.Chat.SystemChatMessage;
using UserChatMessage = OpenAI.Chat.UserChatMessage;
using AssistantChatMessage = OpenAI.Chat.AssistantChatMessage;

namespace StoryGenerator.AI.Services
{
    public class OpenAIService : IOpenAIService
    {
        private readonly OpenAIClient _client;
        private readonly ILogger<OpenAIService> _logger;
        private readonly ResiliencePipeline _retryStrategy;

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
        {
            _logger = logger;

            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI API key is required");

            var baseUrl = configuration["OpenAI:BaseUrl"];
            var timeout = configuration.GetValue<int?>("OpenAI:Timeout");

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = string.IsNullOrEmpty(baseUrl) ? null : new Uri(baseUrl),
                NetworkTimeout = timeout.HasValue ? TimeSpan.FromMilliseconds(timeout.Value) : TimeSpan.FromSeconds(30)
            };

            _client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

            // Setup retry policy using Polly
            _retryStrategy = new ResiliencePipelineBuilder()
                .AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    Delay = TimeSpan.FromSeconds(1),
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential
                })
                .Build();

            _logger.LogInformation("OpenAI client initialized");
        }

        public async Task<string> GenerateTextAsync(
            List<ChatMessage> messages,
            string model = "gpt-3.5-turbo",
            int maxTokens = 2000,
            double temperature = 0.7)
        {
            try
            {
                _logger.LogDebug("Generating text with model: {Model}", model);

                var response = await _retryStrategy.ExecuteAsync(async cancellationToken =>
                {
                    // Map Core ChatMessage -> OpenAI.Chat.ChatMessage
                    var chatMessages = messages.Select(MapToAIChatMessage).ToList();

                    var completion = await _client.GetChatClient(model).CompleteChatAsync(
                        chatMessages,
                        new ChatCompletionOptions
                        {
                            MaxTokens = maxTokens,
                            Temperature = (float)temperature
                        },
                        cancellationToken);

                    return completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
                });

                _logger.LogDebug("Generated {Length} characters", response.Length);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI API Error");
                throw new InvalidOperationException($"Failed to generate text from OpenAI: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateCompletionAsync(
            string prompt,
            string model = "gpt-3.5-turbo",
            int maxTokens = 2000,
            double temperature = 0.7)
        {
            var messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = prompt, Timestamp = DateTime.UtcNow }
            };
            return await GenerateTextAsync(messages, model, maxTokens, temperature);
        }

        public async Task<T> GenerateStructuredOutputAsync<T>(
            List<ChatMessage> messages,
            string model = "gpt-3.5-turbo")
            where T : class
        {
            try
            {
                // Add instruction to return JSON
                var systemMessage = new ChatMessage
                {
                    Role = "system",
                    Content = "You must respond with valid JSON that matches the required schema. Do not include any additional text, formatting, reasoning, or thinking content. Return ONLY the JSON object.",
                    Timestamp = DateTime.UtcNow
                };

                var allMessages = new List<ChatMessage> { systemMessage };
                allMessages.AddRange(messages);

                var response = await GenerateTextAsync(allMessages, model, 2500, 0.2);

                // Try to parse the JSON response
                try
                {
                    return JsonSerializer.Deserialize<T>(response.Trim(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? throw new InvalidOperationException("Deserialization returned null");
                }
                catch (JsonException parseError)
                {
                    _logger.LogError(parseError, "Failed to parse JSON response: {Response}", response);
                    // Try using enhanced JSON parser as fallback
                    return ParsePartialJson<T>(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Structured output generation error");
                throw;
            }
        }

        public async Task<string> GenerateWithHistoryAsync(
            List<ChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gpt-3.5-turbo",
            int maxTokens = 2000)
        {
            var messages = new List<ChatMessage>();

            // Add system prompt if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content = systemPrompt,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Add conversation history
            messages.AddRange(sessionMessages);

            // Add new user message
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = newUserMessage,
                Timestamp = DateTime.UtcNow
            });

            return await GenerateTextAsync(messages, model, maxTokens);
        }

        public async Task<T> GenerateStructuredWithHistoryAsync<T>(
            List<ChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gpt-3.5-turbo")
            where T : class
        {
            var messages = new List<ChatMessage>();

            // Add system prompt if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content = systemPrompt,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Add conversation history
            messages.AddRange(sessionMessages);

            // Add new user message
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = newUserMessage,
                Timestamp = DateTime.UtcNow
            });

            return await GenerateStructuredOutputAsync<T>(messages, model);
        }

        private T ParsePartialJson<T>(string jsonString) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("Deserialization returned null");
            }
            catch
            {
                _logger.LogError("Initial JSON parse failed, attempting to fix...");

                // Try to fix common issues with partial JSON
                var fixedJson = jsonString.Trim();

                // Remove <think> tags and similar reasoning content
                fixedJson = System.Text.RegularExpressions.Regex.Replace(fixedJson, @"<think>[\s\S]*?</think>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                fixedJson = fixedJson.Trim();

                // Remove markdown code blocks
                var markdownMatch = System.Text.RegularExpressions.Regex.Match(fixedJson, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (markdownMatch.Success)
                {
                    fixedJson = markdownMatch.Groups[1].Value.Trim();
                }

                // Try to find JSON object boundaries
                var firstBrace = fixedJson.IndexOf('{');
                var lastBrace = fixedJson.LastIndexOf('}');

                if (firstBrace != -1 && lastBrace != -1 && lastBrace > firstBrace)
                {
                    fixedJson = fixedJson.Substring(firstBrace, lastBrace - firstBrace + 1);
                }

                try
                {
                    var result = JsonSerializer.Deserialize<T>(fixedJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    _logger.LogInformation("Successfully parsed JSON after fixes");
                    return result ?? throw new InvalidOperationException("Deserialization returned null");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON after all attempts");
                    throw new InvalidOperationException($"Unable to parse JSON: {jsonString[..Math.Min(200, jsonString.Length)]}...");
                }
            }
        }

        // Map your Core ChatMessage to the SDK ChatMessage
        private static AIChatMessage MapToAIChatMessage(ChatMessage m)
        {
            var role = (m.Role ?? string.Empty).Trim().ToLowerInvariant();
            var content = m.Content ?? string.Empty;

            return role switch
            {
                "system" => new SystemChatMessage(content),
                "assistant" => new AssistantChatMessage(content),
                _ => new UserChatMessage(content)
            };
        }

        // Forward explicit interface implementations so DI callers using the interface work
        Task<string> IOpenAIService.GenerateTextAsync(List<ChatMessage> messages, string model, int maxTokens, double temperature)
            => GenerateTextAsync(messages, model, maxTokens, temperature);

        Task<T> IOpenAIService.GenerateStructuredOutputAsync<T>(List<ChatMessage> messages, string model)
            => GenerateStructuredOutputAsync<T>(messages, model);

        Task<string> IOpenAIService.GenerateWithHistoryAsync(List<ChatMessage> sessionMessages, string newUserMessage, string? systemPrompt, string model, int maxTokens)
            => GenerateWithHistoryAsync(sessionMessages, newUserMessage, systemPrompt, model, maxTokens);

        Task<T> IOpenAIService.GenerateStructuredWithHistoryAsync<T>(List<ChatMessage> sessionMessages, string newUserMessage, string? systemPrompt, string model)
            => GenerateStructuredWithHistoryAsync<T>(sessionMessages, newUserMessage, systemPrompt, model);
    }
}