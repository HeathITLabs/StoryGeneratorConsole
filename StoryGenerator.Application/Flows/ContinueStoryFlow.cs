// Pseudocode / Plan:
// - Ensure missing model types are present so the flow compiles.
// - Define ContinueStoryFlowInput and ContinueStoryFlowOutput used by FlowBase generic args.
// - Define ContinueStoryResponse and ChoiceDto to match AI structured response.
// - Keep original flow logic unchanged; use the newly defined types.
// - Add required usings.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StoryGenerator.AI.Services;
using StoryGenerator.Core.Constants;
using StoryGenerator.Core.Models;

namespace StoryGeneratorConsole.StoryGenerator.Application.Flows
{
    public class ContinueStoryFlow : FlowBase<ContinueStoryFlowInput, ContinueStoryFlowOutput>
    {
        public ContinueStoryFlow(IOpenAIService openAI, ISessionStore sessions, ILogger<ContinueStoryFlow> logger)
            : base(openAI, sessions, logger) { }

        public override async Task<ContinueStoryFlowOutput> ExecuteAsync(ContinueStoryFlowInput input, CancellationToken cancellationToken = default)
        {
            var state = Sessions.GetOrCreate(input.SessionId);

            Sessions.AppendMessage(input.SessionId, User(input.UserInput));

            var history = string.Join("\n", state.StoryParts.TakeLast(5));
            var messages = new List<ChatMessage>
            {
                Sys(StoryPrompts.PreamblePrompt),
                User($@"Continue the story from this recent context:

{history}

User choice or input: ""{input.UserInput}"".

Return JSON with:
- storyParts: array of strings (1-2 segments advancing the story)
- rating: ""GOOD""|""NEUTRAL""|""BAD"" on the user's choice
- primaryObjective: string
- achievedCurrentMilestone: boolean
- progress: number 0..1 toward objective
- choices: array of objects: {{ choice: string, rating: ""GOOD""|""NEUTRAL""|""BAD"" }}")
            };

            var cont = await OpenAI.GenerateStructuredOutputAsync<ContinueStoryResponse>(messages);

            Sessions.Update(input.SessionId, s =>
            {
                s.StoryParts.AddRange(cont.StoryParts);
                s.PrimaryObjective = cont.PrimaryObjective;
                s.Options.Clear();
                s.Options.AddRange(cont.Choices.Select(c => c.Choice));
                s.Progress = cont.Progress;
                s.Rating = cont.Rating;
                s.Messages.AddRange(messages);
            });

            return new ContinueStoryFlowOutput
            {
                StoryParts = cont.StoryParts,
                Options = cont.Choices.Select(c => c.Choice).ToList(),
                PrimaryObjective = cont.PrimaryObjective,
                Progress = cont.Progress,
                Rating = cont.Rating
            };
        }
    }

    // Models added to resolve missing type errors and to represent AI structured response.

    public class ContinueStoryFlowInput
    {
        [Required]
        public string UserInput { get; set; } = string.Empty;

        [Required]
        public string SessionId { get; set; } = string.Empty;
    }

    public class ContinueStoryFlowOutput
    {
        public List<string> StoryParts { get; set; } = new();
        public List<string> Options { get; set; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public double Progress { get; set; }
        public string Rating { get; set; } = string.Empty;
    }

    // Response shape expected from OpenAI.GenerateStructuredOutputAsync for this flow.
    internal class ContinueStoryResponse
    {
        public List<string> StoryParts { get; set; } = new();
        public string Rating { get; set; } = string.Empty;
        public string PrimaryObjective { get; set; } = string.Empty;
        public bool AchievedCurrentMilestone { get; set; }
        public double Progress { get; set; }
        public List<ChoiceDto> Choices { get; set; } = new();
    }

    internal class ChoiceDto
    {
        public string Choice { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
    }
}