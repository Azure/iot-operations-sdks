using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Microsoft.Extensions.Logging;

internal class ProcessControlActionServiceMultiple : IAsyncDisposable
{
    private readonly IMqttPubSubClient _mqttClient;
    private readonly ProcessControlActionExecutorMultiple _processControlActionExecutor;
    private readonly string _commandTopic;
    private readonly string _processControlGroupName;
    private readonly string _assetName;
    private readonly HashSet<string> _actionNames;
    private readonly ILogger<ProcessControlActionServiceMultiple> _logger;

    public ProcessControlActionServiceMultiple(
            IMqttPubSubClient mqttClient,
            string mqttCommandTopic,
            string assetName,
            string processControlGroupName,
            IEnumerable<string> actionNames,
            ILogger<ProcessControlActionServiceMultiple> logger)
    {
        _mqttClient = mqttClient;
        _processControlGroupName = processControlGroupName;
        _assetName = assetName;
        _commandTopic = mqttCommandTopic;
        _actionNames = new HashSet<string>(actionNames);

        _processControlActionExecutor = new ProcessControlActionExecutorMultiple(mqttClient)
        {
            OnCommandReceived = ProcessControlAction,
            TopicNamespace = null,
        };
        _processControlActionExecutor.TopicTokenMap["MqttCommandTopic"] = _commandTopic;
        _processControlActionExecutor.TopicTokenMap["ProcessControlGroup"] = _processControlGroupName;

        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var clientId = _mqttClient.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("No MQTT client id configured");
        }

        var transientTokenMap = new Dictionary<string, string>
        {
            { "executorId", clientId }
        };

        await _processControlActionExecutor.StartAsync(
                preferredDispatchConcurrency: null,
                transientTokenMap,
                cancellationToken).ConfigureAwait(false);

        await Task.Run(async () => {
            while (!cancellationToken.IsCancellationRequested)
            {
                // keep the executor running
                await Task.Delay(100).ConfigureAwait(false);
            }
        });

        await _processControlActionExecutor.StopAsync().ConfigureAwait(false);
    }

    private async Task<ExtendedResponse<string>> ProcessControlAction(
            ExtendedRequest<string> extendedRequest,
            CancellationToken cancellationToken)
    {
        var request = extendedRequest.Request;
        var requestMetadata = extendedRequest.RequestMetadata;

        _logger.LogInformation($"Executing DatasetWrite with correlationId {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        _logger.LogDebug($"Asset {_assetName}, ProcessControlGroup {_processControlGroupName}");
        _logger.LogDebug(request.ToString());

        await Task.Delay(100);
        // Simulate a successful response

        return new ExtendedResponse<string>
        {
            Response = "{}",
        };
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
