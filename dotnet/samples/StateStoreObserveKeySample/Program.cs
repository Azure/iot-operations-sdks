using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

var mqttClient = new MqttSessionClient();

MqttConnectionSettings connectionSettings = new("localhost", "someClientId") { TcpPort = 1883, UseTls = false };
MqttClientConnectResult result = await mqttClient.ConnectAsync(connectionSettings);

if (result.ResultCode != MqttClientConnectResultCode.Success)
{
    throw new Exception($"Failed to connect to MQTT broker. Code: {result.ResultCode} Reason: {result.ReasonString}");
}

await using StateStoreClient stateStoreClient = new(new(), mqttClient);
TaskCompletionSource onKeyChange = new TaskCompletionSource();

try
{
    string stateStoreKey = "someKey";
    string stateStoreValue = "someValue";
    string newValue = "someNewValue";

    KeyChangeMessageReceivedEventArgs? mostRecentChange = null;

    // callback to handle key change notifications
    Task OnKeyChange(object? arg1, KeyChangeMessageReceivedEventArgs args)
    {
        Console.WriteLine($"Observed: Key {args.ChangedKey} changed value to {args.NewValue}");
        mostRecentChange = args;
        onKeyChange.TrySetResult();
        return Task.CompletedTask;
    }

    // subscribe to the key change event
    stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChange;

    // observe for notifications when the key changes values
    await stateStoreClient.ObserveAsync(stateStoreKey);

    await SetKeyAndWaitForNotification(stateStoreClient, stateStoreKey, stateStoreValue);
    await SetKeyAndWaitForNotification(stateStoreClient, stateStoreKey, newValue);

    await UnobserveKey(stateStoreClient, stateStoreKey, stateStoreValue);
}

finally
{
    Console.WriteLine("The End.");
}

async Task SetKeyAndWaitForNotification(StateStoreClient client, string key, string value)
{
    Console.WriteLine($"Setting the key to {value}...");
    await client.SetAsync(key, value);
    await onKeyChange.Task;
    onKeyChange = new TaskCompletionSource();
}

async Task UnobserveKey(StateStoreClient client, string key, string value)
{
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    cts.Token.Register(() => onKeyChange.TrySetResult());

    Console.WriteLine("Unobserving the key, setting/deleting the key should not be successfully observed...");
    await client.UnobserveAsync(key);

    Console.WriteLine($"Setting the key to {value}...");
    await client.SetAsync(key, value);
    Console.WriteLine("Deleting the key...");
    await client.DeleteAsync(key);

    await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Console.WriteLine("Successfully unobserved the key.");
}
