using StoryGenerator.Core.Models;

namespace StoryGenerator.Core.Services
{
    public interface IFlowEngine
    {
        void RegisterFlow<TInput, TOutput>(string name, Func<TInput, FlowContext?, Task<TOutput>> handler);
        Task<FlowResponse<TOutput>> ExecuteFlowAsync<TInput, TOutput>(string flowName, FlowRequest<TInput> request);
        Task<TOutput> ExecuteFlowDirectAsync<TInput, TOutput>(string flowName, TInput input, string? sessionId = null);
        List<string> GetRegisteredFlows();
    }
}