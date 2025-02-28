using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using System.Reflection;

namespace Azure.Iot.Operations.Services.DssCli;

public class DssClientService(ILogger<DssClientService> logger, IConfiguration config, OrderedAckMqttClient mqttClient, StateStoreClient dss) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string dsscliCnxFile = Path.Combine(assemblyFolder, "dsscli.cnx");

        string cs = config.GetConnectionString("Default")!;
        
        MqttConnectionSettings mcs;
        if (string.IsNullOrEmpty(cs))
        {
            if (File.Exists(dsscliCnxFile))
            {
                cs = File.ReadAllText(dsscliCnxFile);
                logger.LogWarning("Using connection string from file: {Cs}", cs);
                mcs = MqttConnectionSettings.FromConnectionString(cs);
            }
            else
            {
                logger.LogWarning("No connection string specified, using default mqtt://localhost:1883");
                mcs = new MqttConnectionSettings("localhost") { TcpPort = 1883, UseTls = false };
                cs = mcs.ToString();
            }
        }
        else
        {
            mcs = MqttConnectionSettings.FromConnectionString(cs);
        }
        File.WriteAllText(dsscliCnxFile, cs);

        MqttClientConnectResult connAck = await mqttClient.ConnectAsync(mcs, stoppingToken);
        logger.LogInformation("Connected to MQTT broker {Broker} with result {Result}", mcs.HostName, connAck.ResultCode);

        string? set = config.GetValue<string>("set");
        string? get = config.GetValue<string>("get");
        string? del = config.GetValue<string>("del");

        if (!string.IsNullOrEmpty(set))
        {
            string? setValue = config.GetValue<string>("value");
            string? setFile = config.GetValue<string>("file");

            string content = string.Empty;
            if (!string.IsNullOrEmpty(setValue))
            {
                content = setValue!;
            }
            else if (!string.IsNullOrEmpty(setFile))
            {
                content = await File.ReadAllTextAsync(setFile!, stoppingToken);
            }
            else
            {
                logger.LogError("No value or file specified");
                Console.WriteLine("Err. No value or file specified");
                Console.WriteLine();
                PrintUsage();
                Environment.Exit(1);
            }

            StateStoreSetResponse resp = await dss.SetAsync(set!, content!, null, cancellationToken: stoppingToken);
            if (resp.Success)
            {
                logger.LogInformation("Successfully set key {Set} with value {Content}", set, content);
                Console.WriteLine($"Successfully set key {set} with {content.Length} bytes");
            }
            else
            {
                logger.LogInformation("Failed to set key {Set} with value {Content}", set, content);
                Console.WriteLine($"Failed to set key {set} with error");
            }

        }
        else if (!string.IsNullOrEmpty(get))
        {
            StateStoreGetResponse resp = await dss.GetAsync(get!, cancellationToken: stoppingToken);
            if (resp.Value != null)
            {
                await Console.Out.WriteLineAsync(resp.Value!.GetString());
            }
            else
            {
                Console.WriteLine("No value found");
            }
        }
        else if (!string.IsNullOrEmpty(del))
        {
            StateStoreDeleteResponse resp = await dss.DeleteAsync(del!, null, cancellationToken: stoppingToken);
            if (resp.DeletedItemsCount == 1)
            {
                logger.LogInformation("Successfully deleted key {Del}", del);
                Console.WriteLine($"Successfully deleted key {del}");
            }
            else
            {
                logger.LogInformation("Failed to delete key {Del}", del);
                Console.WriteLine($"Failed to delete key {del}");
            }
        }
        else
        {
            logger.LogWarning("No action specified");
            PrintUsage();
        }
        await mqttClient.DisconnectAsync(new MqttClientDisconnectOptions() { ReasonString = "Command End"}, stoppingToken);
        Environment.Exit(0);
    }

    static void PrintUsage() => Console.WriteLine(
@$"IoTMq DSS CLI Usage: 

dsscli 
--get <key> 
--set <key> -value <value> | --file <file>
--del <key>

[optional settings]
    --mqttDiag false
    --ConnectionStrings:Default 'Hostname=localhost;TcpPort=1883;UseTls=false;'
    --Logging:LogLevel:Default=Information

");
}
