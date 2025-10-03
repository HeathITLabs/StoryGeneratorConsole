using StoryGenerator.Core.Models;

namespace StoryGeneratorConsole.StoryGenerator.Application
{
    // UI-agnostic service contract to drive the game from any client (console, Teams, Discord, web, etc.)
    public interface IStoryGame
    {
        Task<DescriptionFlowOutput> GetPremiseAsync(string sessionId, string? userInput, CancellationToken ct = default);
        Task<BeginStoryFlowOutput> BeginAsync(string sessionId, CancellationToken ct = default);
        Task<StoryGeneratorConsole.StoryGenerator.Application.Flows.ContinueStoryFlowOutput> ContinueAsync(string sessionId, string userInput, CancellationToken ct = default);
        Task<ImageGenerationOutput> GenerateImageAsync(string sessionId, string story, string? style = null, string? theme = null, CancellationToken ct = default);
    }
}
