using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;


var mqttClient = new MqttSessionClient();

MqttConnectionSettings connectionSettings = new("localhost") { TcpPort = 1883, ClientId = "someClientId", UseTls = false };
MqttClientConnectResult result = await mqttClient.ConnectAsync(connectionSettings);

if (result.ResultCode != MqttClientConnectResultCode.Success)
{
    throw new Exception($"Failed to connect to MQTT broker. Code: {result.ResultCode} Reason: {result.ReasonString}");
}

StateStoreClient stateStoreClient = new(mqttClient);

try
{
    string stateStoreKey = "someKey";
    string stateStoreValue = "someValue";
    string newValue = "someNewValue";

    KeyChangeMessageReceivedEventArgs? mostRecentChange = null;
    TaskCompletionSource onKeyChange = new TaskCompletionSource();
    Task OnKeyChange(object? arg1, KeyChangeMessageReceivedEventArgs args)
    {
        Console.WriteLine($"Key {args.ChangedKey} changed to {args.NewValue}");
        mostRecentChange = args;
        onKeyChange.TrySetResult();
        return Task.CompletedTask;
    }

    stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChange;

    // subscribe to notifications when the key changes values
    await stateStoreClient.ObserveAsync(stateStoreKey);

    // change the key value
    Console.WriteLine("Setting the key...");
    await stateStoreClient.SetAsync(stateStoreKey, stateStoreValue);

    // change the key value again
    Console.WriteLine("Setting the key again...");
    await stateStoreClient.SetAsync(stateStoreKey, newValue);

}
finally
{
    await stateStoreClient.DisposeAsync(true);
}
Console.WriteLine("The End.");