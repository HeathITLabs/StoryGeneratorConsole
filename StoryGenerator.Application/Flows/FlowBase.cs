using Microsoft.Extensions.Logging;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Models;

namespace StoryGeneratorConsole.StoryGenerator.Application.Flows
{
    public abstract class FlowBase<TInput, TOutput> : IFlow<TInput, TOutput>
    {
        protected readonly IOpenAIService OpenAI;
        protected readonly ISessionStore Sessions;
        protected readonly ILogger Logger;

        protected FlowBase(IOpenAIService openAI, ISessionStore sessions, ILogger logger)
        {
            OpenAI = openAI;
            Sessions = sessions;
            Logger = logger;
        }

        public abstract Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);

        protected static ChatMessage Sys(string content) => new() { Role = "system", Content = content, Timestamp = DateTime.UtcNow };
        protected static ChatMessage User(string content) => new() { Role = "user", Content = content, Timestamp = DateTime.UtcNow };
        protected static ChatMessage Asst(string content) => new() { Role = "assistant", Content = content, Timestamp = DateTime.UtcNow };
    }
}