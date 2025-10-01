using System.Threading;
using System.Threading.Tasks;

namespace StoryGenerator.AI.Services
{
    public interface IImageGenerationService
    {
        Task<(byte[] Image, string MimeType)> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);
    }
}