// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
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
Console.CancelKeyPress += delegate {
    logger.LogInformation("SIGINT detected - closing application");
    cancellationTokenSource.Cancel();
};

var sessionClientOptions = new MqttSessionClientOptions {
    EnableMqttLogging = true,
};

var mqttClient = new MqttSessionClient(sessionClientOptions);

var connectionString = configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString)) {
    logger.LogError("Please provide connection string via appsettings.json, environment variable or command line argument");
    Environment.FailFast(null);
}
var mqttConnectionSettings = MqttConnectionSettings.FromConnectionString(
        connectionString);

var mqttConnectResult = await mqttClient.ConnectAsync(
        mqttConnectionSettings,
        cancellationTokenSource.Token).ConfigureAwait(false);

if (!mqttConnectResult.IsSessionPresent) {
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

var datasetWriteDefaulTask = dataSetWriteServiceDefault.StartAsync(cancellationTokenSource.Token);
var datasetWriteUNSTask = dataSetWriteServiceUNS.StartAsync(cancellationTokenSource.Token);

await Task.WhenAny(
    datasetWriteDefaulTask,
    datasetWriteUNSTask).ConfigureAwait(false);

logger.LogInformation("Application stopped!");
