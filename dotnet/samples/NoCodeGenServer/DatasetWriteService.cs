using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Microsoft.Extensions.Logging;

internal class DatasetWriteService : IAsyncDisposable
{
    private readonly IMqttPubSubClient _mqttClient;
    private readonly DatasetWriteExecutor _datasetWriteExecutor;
    private readonly string _commandTopic;
    private readonly string _assetName;
    private readonly string _datasetName;
    private readonly ILogger<DatasetWriteService> _logger;

    public DatasetWriteService(
            IMqttPubSubClient mqttClient,
            string mqttCommandTopic,
            string assetName,
            string datasetName,
            ILogger<DatasetWriteService> logger)
    {
        _mqttClient = mqttClient;
        _assetName = assetName;
        _commandTopic = mqttCommandTopic;
        _datasetName = datasetName;

        _datasetWriteExecutor = new DatasetWriteExecutor(mqttClient)
        {
            OnCommandReceived = DatasetWrite,
            TopicNamespace = null,
            ExecutionTimeout = TimeSpan.FromMinutes(1)
        };
        _datasetWriteExecutor.TopicTokenMap["MqttCommandTopic"] = _commandTopic;

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
            { "executorId", $"dataset-write-{_assetName}-{_datasetName}" }
        };

        await _datasetWriteExecutor.StartAsync(
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

        await _datasetWriteExecutor.StopAsync().ConfigureAwait(false);
    }

    private async Task<ExtendedResponse<DatasetWriteResponse>> DatasetWrite(
            ExtendedRequest<DatasetWriteRequest> extendedRequest,
            CancellationToken cancellationToken)
    {
        var request = extendedRequest.Request;
        var requestMetadata = extendedRequest.RequestMetadata;

        _logger.LogInformation($"Executing DatasetWrite with correlationId {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        _logger.LogDebug($"Asset {_assetName}, Dataset {_datasetName}");
        _logger.LogTrace(request.ToString());

        await Task.Delay(100);
        // Simulate a successful response
        Dictionary<string, string> writeResponse = new Dictionary<string, string>();

        if (request.DataValues.ContainsKey("BadFastUInt1"))
        {
            writeResponse.Add("BadFastUInt1", "Bad_NodeIdUnknown");
        }

        return new ExtendedResponse<DatasetWriteResponse>
        {
            Response = new DatasetWriteResponse(writeResponse)
        };
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
