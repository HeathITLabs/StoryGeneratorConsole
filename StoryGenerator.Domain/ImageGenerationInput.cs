namespace StoryGenerator.Core.Models
{
    public class ImageGenerationInput
    {
        public string SessionId { get; set; } = default!;
        public string Story { get; set; } = default!;

        // Optional: illustration style preset (e.g., "Graphic Novel", "Watercolor", etc.)
        public string? Style { get; set; }

        // Optional: mood/keywords (e.g., "dark fantasy, steampunk")
        public string? Theme { get; set; }
    }
}
