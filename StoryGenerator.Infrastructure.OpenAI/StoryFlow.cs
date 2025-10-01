using System.ComponentModel.DataAnnotations;

namespace StoryGenerator.Core.Models
{
    public class FlowRequest<TInput>
    {
        public TInput Input { get; set; } = default!;
        public string? SessionId { get; set; }
    }

    public class FlowResponse<TOutput>
    {
        public TOutput? Result { get; set; }
        public string? SessionId { get; set; }
        public string? Error { get; set; }
        public bool IsSuccess => Error == null;
    }

    public class FlowContext
    {
        public string? SessionId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public interface IFlowDefinition<TInput, TOutput>
    {
        string Name { get; }
        Task<TOutput> ExecuteAsync(TInput input, FlowContext? context = null);
    }
}