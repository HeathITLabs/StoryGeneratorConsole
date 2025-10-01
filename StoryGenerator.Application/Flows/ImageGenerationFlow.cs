using Microsoft.Extensions.Logging;
using StoryGenerator.Core.Models;
using StoryGenerator.AI.Services;

namespace StoryGeneratorConsole.StoryGenerator.Application.Flows
{
    public class ImageGenerationFlow : FlowBase<ImageGenerationInput, ImageGenerationOutput>
    {
        private readonly IImageGenerationService _imageService;

        public ImageGenerationFlow(IOpenAIService openAI, ISessionStore sessions, IImageGenerationService imageService, ILogger<ImageGenerationFlow> logger)
            : base(openAI, sessions, logger)
        {
            _imageService = imageService;
        }

        public override async Task<ImageGenerationOutput> ExecuteAsync(ImageGenerationInput input, CancellationToken cancellationToken = default)
        {
            var state = Sessions.GetOrCreate(input.SessionId);

            var scene = string.IsNullOrWhiteSpace(input.Story)
                ? state.StoryParts.LastOrDefault() ?? "A captivating story scene"
                : input.Story;

            // Simple image prompt
            var prompt = $"Highly detailed illustration, cinematic lighting, concept art, {scene}";

            var (image, mime) = await _imageService.GenerateImageAsync(prompt, cancellationToken);

            var imagesDir = Path.Combine(AppContext.BaseDirectory, "images");
            Directory.CreateDirectory(imagesDir);
            var file = Path.Combine(imagesDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");
            await File.WriteAllBytesAsync(file, image, cancellationToken);

            Sessions.Update(input.SessionId, s => s.ImagePaths.Add(file));

            return new ImageGenerationOutput { FilePath = file, MimeType = mime };
        }
    }
}