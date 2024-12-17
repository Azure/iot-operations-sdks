using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using System.Text;

namespace ConnectionManagementSample;

public class LibraryManagedConnectionWorker(MqttSessionClient sessionClient, ILogger<LibraryManagedConnectionWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        sessionClient.SessionLostAsync += OnCrash;
        sessionClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")! + ";ClientId=LibraryManagedConnectionClient-" + Guid.NewGuid());

        await sessionClient.ConnectAsync(mcs, cancellationToken);

        var subscribe = new MqttClientSubscribeOptions("libraryManagedConnectionSampleTopic/hello", MqttQualityOfServiceLevel.AtLeastOnce);
        await sessionClient.SubscribeAsync(subscribe, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await sessionClient.PublishAsync(
                new MqttApplicationMessage("userManagedConnectionSampleTopic/hello")
                { 
                    PayloadSegment = Encoding.UTF8.GetBytes("hello from the library-managed connection sample!"),
                }, 
                cancellationToken);

            await Task.Delay(10000, cancellationToken);
        }

        await sessionClient.DisconnectAsync(null, cancellationToken);
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        logger.LogInformation("Received a message with topic {t} with payload {p}", args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString());

        // You can also acknowledge a message manually later via the args.AcknowledgeAsync() API
        args.AutoAcknowledge = true;

        return Task.CompletedTask;
    }

    // Unlike with the user-managed connection code, this callback is only executed on fatal errors. Any non-fatal
    // error will be handled by the session client instead of reporting it to the application layer to handle.
    private Task OnCrash(MqttClientDisconnectedEventArgs args)
    {
        logger.LogWarning("The session client encountered a fatal error and is no longer connected. {ex}", args.Exception);
        return Task.CompletedTask;
    }
}
