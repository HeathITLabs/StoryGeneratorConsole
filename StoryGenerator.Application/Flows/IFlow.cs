using System.Threading;
using System.Threading.Tasks;

namespace StoryGeneratorConsole.StoryGenerator.Application.Flows
{
    public interface IFlow<in TInput, TOutput>
    {
        Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
    }
}