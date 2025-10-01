using System.ComponentModel.DataAnnotations;

namespace StoryGenerator.Core.Models
{
    public class ImageGenerationOutput
    {
        public string FilePath { get; set; } = string.Empty;
        public string MimeType { get; set; } = "image/png";
    }
}