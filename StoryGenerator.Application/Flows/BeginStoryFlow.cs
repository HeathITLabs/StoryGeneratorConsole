using Microsoft.Extensions.Logging;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Constants;
using StoryGenerator.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace StoryGeneratorConsole.StoryGenerator.Application.Flows
{
    public class BeginStoryFlow : FlowBase<BeginStoryFlowInput, BeginStoryFlowOutput>
    {
        public BeginStoryFlow(IOpenAIService openAI, ISessionStore sessions, ILogger<BeginStoryFlow> logger)
            : base(openAI, sessions, logger) { }

        public override async Task<BeginStoryFlowOutput> ExecuteAsync(BeginStoryFlowInput input, CancellationToken cancellationToken = default)
        {
            var state = Sessions.GetOrCreate(input.SessionId);

            Sessions.AppendMessage(input.SessionId, User(input.UserInput));

            var messages = new List<ChatMessage>
            {
                Sys(StoryPrompts.PreamblePrompt),
                User($@"Start the story. Return JSON with:
- storyParts: array of strings (1-3 segments of the opening)
- primaryObjective: string
- progress: number between 0 and 1 where 0=just started
- choices: array of objects: {{ choice: string, rating: ""GOOD""|""NEUTRAL""|""BAD"" }}

Consider the conversation so far and the user's latest input: ""{input.UserInput}"".")
            };

            var detail = await OpenAI.GenerateStructuredOutputAsync<StoryDetailResponse>(messages);

            Sessions.Update(input.SessionId, s =>
            {
                s.StoryParts.AddRange(detail.StoryParts);
                s.PrimaryObjective = detail.PrimaryObjective;
                s.Options.Clear();
                s.Options.AddRange(detail.Choices.Select(c => c.Choice));
                s.Progress = detail.Progress;
                s.Messages.AddRange(messages);
            });

            return new BeginStoryFlowOutput
            {
                StoryParts = detail.StoryParts,
                Options = detail.Choices.Select(c => c.Choice).ToList(),
                PrimaryObjective = detail.PrimaryObjective,
                Progress = detail.Progress
            };
        }
    }
}

namespace StoryGenerator.Core.Models
{
    public class BeginStoryFlowInput
    {
        [Required]
        public string UserInput { get; set; } = string.Empty;

        [Required]
        public string SessionId { get; set; } = string.Empty;
    }

    public class BeginStoryFlowOutput
    {
        public List<string> StoryParts { get; set; } = new();
        public List<string> Options { get; set; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public double Progress { get; set; }
    }

    // Structured response expected from the LLM
    public class StoryDetailResponse
    {
        public List<string> StoryParts { get; set; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public double Progress { get; set; }
        public List<ChoiceRating> Choices { get; set; } = new();
    }

    public class ChoiceRating
    {
        public string Choice { get; set; } = string.Empty;
        public string Rating { get; set; } = "NEUTRAL";
    }
}