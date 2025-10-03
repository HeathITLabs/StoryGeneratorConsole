using StoryGenerator.Core.Models;
using StoryGeneratorConsole.StoryGenerator.Application;
using StoryGeneratorConsole.StoryGenerator.Application.Flows;

namespace StoryGenerator.Presentation.Console
{
    // Orchestrates flows; contains no UI code
    public sealed class StoryGameService : IStoryGame
    {
        private readonly IFlowEngine _engine;

        public StoryGameService(IFlowEngine engine)
        {
            _engine = engine;
        }

        public Task<DescriptionFlowOutput> GetPremiseAsync(string sessionId, string? userInput, CancellationToken ct = default)
            => _engine.ExecuteAsync<DescriptionFlowInput, DescriptionFlowOutput>(
                FlowNames.Description,
                new DescriptionFlowInput
                {
                    UserInput = userInput,
                    SessionId = sessionId,
                    ClearSession = false
                },
                ct);

        public Task<BeginStoryFlowOutput> BeginAsync(string sessionId, CancellationToken ct = default)
            => _engine.ExecuteAsync<BeginStoryFlowInput, BeginStoryFlowOutput>(
                FlowNames.Begin,
                new BeginStoryFlowInput
                {
                    UserInput = "Begin the story.",
                    SessionId = sessionId
                },
                ct);

        public Task<ContinueStoryFlowOutput> ContinueAsync(string sessionId, string userInput, CancellationToken ct = default)
            => _engine.ExecuteAsync<ContinueStoryFlowInput, ContinueStoryFlowOutput>(
                FlowNames.Continue,
                new ContinueStoryFlowInput
                {
                    UserInput = userInput,
                    SessionId = sessionId
                },
                ct);

        public Task<ImageGenerationOutput> GenerateImageAsync(string sessionId, string story, string? style = null, string? theme = null, CancellationToken ct = default)
            => _engine.ExecuteAsync<ImageGenerationInput, ImageGenerationOutput>(
                FlowNames.Image,
                new ImageGenerationInput
                {
                    Story = story,
                    SessionId = sessionId,
                    Style = style,
                    Theme = theme
                },
                ct);
    }
}
