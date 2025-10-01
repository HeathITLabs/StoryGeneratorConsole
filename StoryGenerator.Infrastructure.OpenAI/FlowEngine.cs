using Microsoft.Extensions.Logging;
using StoryGenerator.Core.Models;
using System.Collections.Concurrent;

namespace StoryGenerator.Core.Services
{
    public class FlowEngine : IFlowEngine
    {
        private readonly ConcurrentDictionary<string, Func<object, FlowContext?, Task<object>>> _flows = new();
        private readonly ISessionStore _sessionStore;
        private readonly ILogger<FlowEngine> _logger;

        public FlowEngine(ISessionStore sessionStore, ILogger<FlowEngine> logger)
        {
            _sessionStore = sessionStore;
            _logger = logger;
        }

        public void RegisterFlow<TInput, TOutput>(string name, Func<TInput, FlowContext?, Task<TOutput>> handler)
        {
            _flows[name] = async (input, context) =>
            {
                if (input is TInput typedInput)
                {
                    var result = await handler(typedInput, context);
                    return result!;
                }
                throw new ArgumentException($"Input type mismatch for flow '{name}'");
            };

            _logger.LogInformation("Registered flow: {FlowName}", name);
        }

        public async Task<FlowResponse<TOutput>> ExecuteFlowAsync<TInput, TOutput>(string flowName, FlowRequest<TInput> request)
        {
            if (!_flows.TryGetValue(flowName, out var handler))
            {
                var error = $"Flow '{flowName}' not found";
                _logger.LogError(error);
                return new FlowResponse<TOutput>
                {
                    Result = default,
                    Error = error
                };
            }

            try
            {
                _logger.LogDebug("Executing flow: {FlowName}", flowName);

                // Create or get session
                var sessionId = request.SessionId;
                if (string.IsNullOrEmpty(sessionId))
                {
                    sessionId = _sessionStore.CreateSession();
                }
                else if (_sessionStore.GetSession(sessionId) == null)
                {
                    sessionId = _sessionStore.CreateSession(sessionId);
                }

                var context = new FlowContext
                {
                    SessionId = sessionId,
                    Metadata = new Dictionary<string, object>()
                };

                // Execute flow
                var result = await handler(request.Input!, context);

                _logger.LogDebug("Flow '{FlowName}' completed successfully", flowName);
                return new FlowResponse<TOutput>
                {
                    Result = (TOutput)result,
                    SessionId = sessionId
                };
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                _logger.LogError(ex, "Flow '{FlowName}' error", flowName);
                return new FlowResponse<TOutput>
                {
                    Result = default,
                    SessionId = request.SessionId,
                    Error = errorMessage
                };
            }
        }

        public async Task<TOutput> ExecuteFlowDirectAsync<TInput, TOutput>(string flowName, TInput input, string? sessionId = null)
        {
            var request = new FlowRequest<TInput>
            {
                Input = input,
                SessionId = sessionId
            };

            var response = await ExecuteFlowAsync<TInput, TOutput>(flowName, request);

            if (!response.IsSuccess)
            {
                throw new InvalidOperationException(response.Error);
            }

            return response.Result!;
        }

        public List<string> GetRegisteredFlows()
        {
            return _flows.Keys.ToList();
        }
    }
}