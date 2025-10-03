using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Polly;
using StoryGenerator.Core.Models;
using System.Text.Json;
using System.ClientModel;
using System.Threading;
using System.Linq;

// Add these aliases to avoid type name collisions
using AIChatMessage = OpenAI.Chat.ChatMessage;
using SystemChatMessage = OpenAI.Chat.SystemChatMessage;
using UserChatMessage = OpenAI.Chat.UserChatMessage;
using AssistantChatMessage = OpenAI.Chat.AssistantChatMessage;
using CoreChatMessage = StoryGenerator.Core.Models.ChatMessage;

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
            List<CoreChatMessage> messages,
            string model = "gemma3",
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
            string model = "gemma3",
            int maxTokens = 2000,
            double temperature = 0.7)
        {
            var messages = new List<CoreChatMessage>
            {
                new() { Role = "user", Content = prompt, Timestamp = DateTime.UtcNow }
            };
            return await GenerateTextAsync(messages, model, maxTokens, temperature);
        }

        public async Task<T> GenerateStructuredOutputAsync<T>(
            List<CoreChatMessage> messages,
            string model = "gemma3")
            where T : class
        {
            try
            {
                // Add instruction to return JSON
                var systemMessage = new CoreChatMessage
                {
                    Role = "system",
                    Content = "Respond with a single valid JSON object that matches the required schema. No extra text, no markdown/code fences, no comments.",
                    Timestamp = DateTime.UtcNow
                };

                var allMessages = new List<CoreChatMessage> { systemMessage };
                allMessages.AddRange(messages);

                // Call chat without forcing response_format (SDK variant may not support it); rely on prompt + post-processing
                var raw = await _retryStrategy.ExecuteAsync(async cancellationToken =>
                {
                    var chatMessages = allMessages.Select(MapToAIChatMessage).ToList();

                    var options = new ChatCompletionOptions
                    {
                        Temperature = 0.2f,
                        MaxTokens = 2500
                    };

                    var completion = await _client
                        .GetChatClient(model)
                        .CompleteChatAsync(chatMessages, options, cancellationToken);

                    return completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
                });

                // Strip any leftover fences just in case and deserialize
                var json = ExtractJsonPayload(raw);

                try
                {
                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? throw new InvalidOperationException("Deserialization returned null");
                }
                catch (JsonException parseError)
                {
                    _logger.LogError(parseError, "Failed to parse JSON response: {Response}", raw);
                    // Fallback to tolerant parser
                    return ParsePartialJson<T>(raw);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Structured output generation error");
                throw;
            }
        }

        public async Task<string> GenerateWithHistoryAsync(
            List<CoreChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gemma3",
            int maxTokens = 2000)
        {
            var messages = new List<CoreChatMessage>();

            // Add system prompt if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new CoreChatMessage
                {
                    Role = "system",
                    Content = systemPrompt,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Add conversation history
            messages.AddRange(sessionMessages);

            // Add new user message
            messages.Add(new CoreChatMessage
            {
                Role = "user",
                Content = newUserMessage,
                Timestamp = DateTime.UtcNow
            });

            return await GenerateTextAsync(messages, model, maxTokens);
        }

        public async Task<T> GenerateStructuredWithHistoryAsync<T>(
            List<CoreChatMessage> sessionMessages,
            string newUserMessage,
            string? systemPrompt = null,
            string model = "gemma3")
            where T : class
        {
            var messages = new List<CoreChatMessage>();

            // Add system prompt if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new CoreChatMessage
                {
                    Role = "system",
                    Content = systemPrompt,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Add conversation history
            messages.AddRange(sessionMessages);

            // Add new user message
            messages.Add(new CoreChatMessage
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

        // Strips ``` or ```json fences and extracts the first balanced JSON object/array.
        private static string ExtractJsonPayload(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            var s = raw.Trim();

            if (s.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNl = s.IndexOf('\n');
                if (firstNl >= 0) s = s[(firstNl + 1)..];

                var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0) s = s[..lastFence];

                return s.Trim();
            }

            int startObj = s.IndexOf('{');
            int startArr = s.IndexOf('[');
            int start = (startObj >= 0 && (startArr == -1 || startObj < startArr)) ? startObj : startArr;

            if (start >= 0)
            {
                char open = s[start];
                char close = open == '{' ? '}' : ']';
                int depth = 0;
                bool inString = false;
                bool escape = false;

                for (int i = start; i < s.Length; i++)
                {
                    char c = s[i];
                    if (inString)
                    {
                        if (escape) escape = false;
                        else if (c == '\\') escape = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (c == '"') { inString = true; continue; }
                    if (c == open) depth++;
                    else if (c == close)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return s.Substring(start, i - start + 1).Trim();
                        }
                    }
                }

                return s[start..].Trim();
            }

            return s;
        }

        // Map your Core ChatMessage to the SDK ChatMessage
        private static AIChatMessage MapToAIChatMessage(CoreChatMessage m)
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
    }
}