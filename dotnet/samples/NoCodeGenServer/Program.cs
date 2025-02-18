// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// USE SDK classes without code-gen to provide feedback to the team

// setup logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("NoCodeGenServer");

// load configuration
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// cancellation support
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += delegate
{
    logger.LogInformation("SIGINT detected - closing application");
    cancellationTokenSource.Cancel();
};

var sessionClientOptions = new MqttSessionClientOptions
{
    EnableMqttLogging = true,
};

var mqttClient = new MqttSessionClient(sessionClientOptions);

var connectionString = configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    logger.LogError("Please provide connection string via appsettings.json, environment variable or command line argument");
    Environment.FailFast(null);
}
var mqttConnectionSettings = MqttConnectionSettings.FromConnectionString(
        connectionString);

var mqttConnectResult = await mqttClient.ConnectAsync(
        mqttConnectionSettings,
        cancellationTokenSource.Token).ConfigureAwait(false);

if (mqttConnectResult.ResultCode != MqttClientConnectResultCode.Success)
{
    logger.LogError($"Connection to MQTT Broker with settings: {mqttConnectionSettings} failed with {mqttConnectResult.ReasonString}");
    Environment.FailFast(null);
}

logger.LogInformation("Successfully connected to MQTT Broker");

var dataSetWriteServiceDefault = new DatasetWriteService(
    mqttClient,
    "AioNamespace/asset-operations/MyAssetId/DatasetName",
    "MyAssetId",
    "MyDatasetName",
    loggerFactory.CreateLogger<DatasetWriteService>());

var dataSetWriteServiceUNS = new DatasetWriteService(
    mqttClient,
    "contosomanufacturing/munich/painintarea/painintcell/robot23",
    "robot23",
    "default",
    loggerFactory.CreateLogger<DatasetWriteService>());

// var processControlActionDefault = new ProcessControlActionServiceMultiple(
//         mqttClient,
//         "AioNamespace/asset-operations/MyAssetId/ProcessControlGroup",
//         "MyAssetId",
//         "ProcessControlGroup",
//         new[] { "foobar", "myAction", "demo" },
//         loggerFactory.CreateLogger<ProcessControlActionServiceMultiple>());
//
var processControlActionSingleDefault = new ProcessControlActionServiceSingle(
         mqttClient,
         "AioNamespace/asset-operations",
         "MyAssetId",
         "ProcessControlGroup",
         "foobar",
         loggerFactory.CreateLogger<ProcessControlActionServiceSingle>());

var datasetWriteDefaulTask = dataSetWriteServiceDefault
    .RunAsync(cancellationTokenSource.Token)
    .ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            logger.LogError($"DatasetWrite Server w/ default topic failed: {t.Exception}");
        }
    });
var datasetWriteUNSTask = dataSetWriteServiceUNS
    .RunAsync(cancellationTokenSource.Token)
    .ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            logger.LogError($"DatasetWrite Server w/ UNS topic failed: {t.Exception}");
        }
    });
// var processControlTask = processControlActionDefault
//     .RunAsync(cancellationTokenSource.Token)
//     .ContinueWith(t =>
//     {
//         if (t.IsFaulted)
//         {
//             logger.LogError($"ProcessControlAction Server w/ default topic failed: {t.Exception}");
//         }
//     });

var processControlActionSingleTask = processControlActionSingleDefault
    .RunAsync(cancellationTokenSource.Token)
    .ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            logger.LogError($"ProcessControlActionSingle Server w/ default topic failed: {t.Exception}");
        }
    });

await Task.WhenAll(
    datasetWriteDefaulTask,
    datasetWriteUNSTask,
    processControlActionSingleTask).ConfigureAwait(false);

logger.LogInformation("Application stopped!");
