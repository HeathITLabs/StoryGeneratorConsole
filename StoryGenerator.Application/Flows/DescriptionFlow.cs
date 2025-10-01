using Microsoft.Extensions.Logging;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Constants;
using StoryGenerator.Core.Models;

namespace StoryGeneratorConsole.StoryGenerator.Application.Flows
{
    public class DescriptionFlow : FlowBase<DescriptionFlowInput, DescriptionFlowOutput>
    {
        public DescriptionFlow(IOpenAIService openAI, ISessionStore sessions, ILogger<DescriptionFlow> logger)
            : base(openAI, sessions, logger) { }

        public override async Task<DescriptionFlowOutput> ExecuteAsync(DescriptionFlowInput input, CancellationToken cancellationToken = default)
        {
            if (input.ClearSession)
            {
                Sessions.Clear(input.SessionId);
            }

            var state = Sessions.GetOrCreate(input.SessionId);

            if (!string.IsNullOrEmpty(input.UserInput))
            {
                Sessions.AppendMessage(input.SessionId, User(input.UserInput));
            }

            var messages = new List<ChatMessage>
            {
                Sys(StoryPrompts.PreamblePrompt),
                User($@"Build or refine the premise. Return JSON with:
- storyPremise: string
- nextQuestion: string
- premiseOptions: array of strings (0-5 items)

User latest input: ""{input.UserInput ?? ""}"".
If insufficient info, invent reasonable details, but keep asking specific next questions.")
            };

            var result = await OpenAI.GenerateStructuredOutputAsync<DescriptionFlowOutput>(messages);

            // Persist minimal context
            Sessions.Update(input.SessionId, s =>
            {
                s.Messages.AddRange(messages);
            });

            return result;
        }
    }
}