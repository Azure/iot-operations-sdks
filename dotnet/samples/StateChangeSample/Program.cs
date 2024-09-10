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

    // define callback for when the key changes
    async Task stateCallback(object? sender, KeyChangeMessageReceivedEventArgs args)
    {
        Console.WriteLine($"Key {args.ChangedKey} changed to {args.NewValue}");
        await Task.CompletedTask;
    }

    // subscribe to notifications when the key changes values
    await stateStoreClient.ObserveAsync(stateStoreKey, stateCallback);

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