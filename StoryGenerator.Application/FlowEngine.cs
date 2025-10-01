using Microsoft.Extensions.DependencyInjection;
using StoryGeneratorConsole.StoryGenerator.Application.Flows;

namespace StoryGeneratorConsole.StoryGenerator.Application
{
    public interface IFlowEngine
    {
        Task<TOut> ExecuteAsync<TIn, TOut>(string flowName, TIn input, CancellationToken cancellationToken = default);
    }

    public class FlowEngine : IFlowEngine
    {
        private readonly IServiceProvider _services;
        private readonly Dictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase);

        public FlowEngine(IServiceProvider services)
        {
            _services = services;

            // Register known flows (name -> type) without binding to specific TIn/TOut at compile time
            Register<DescriptionFlow>("Description");
            Register<BeginStoryFlow>("Begin");
            Register<ContinueStoryFlow>("Continue");
            Register<ImageGenerationFlow>("Image");
        }

        // New overload: register by flow type only to avoid compile-time dependency on TIn/TOut
        public void Register<TFlow>(string name)
        {
            _registry[name] = typeof(TFlow);
        }

        // Existing overload remains for scenarios where TIn/TOut are known and desired
        public void Register<TFlow, TIn, TOut>(string name) where TFlow : IFlow<TIn, TOut>
        {
            _registry[name] = typeof(TFlow);
        }

        public async Task<TOut> ExecuteAsync<TIn, TOut>(string flowName, TIn input, CancellationToken cancellationToken = default)
        {
            if (!_registry.TryGetValue(flowName, out var flowType))
                throw new InvalidOperationException($"Flow '{flowName}' not registered.");

            var flow = _services.GetRequiredService(flowType) as IFlow<TIn, TOut>
                ?? throw new InvalidOperationException($"Flow '{flowName}' does not match expected signature.");

            return await flow.ExecuteAsync(input, cancellationToken);
        }
    }
}