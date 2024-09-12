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

await using StateStoreClient stateStoreClient = new(mqttClient);

try
{
    string stateStoreKey = "someKey";
    string stateStoreValue = "someValue";
    string newValue = "someNewValue";

    KeyChangeMessageReceivedEventArgs? mostRecentChange = null;
    TaskCompletionSource onKeyChange = new TaskCompletionSource();

    // callback to handle key change notifications
    Task OnKeyChange(object? arg1, KeyChangeMessageReceivedEventArgs args)
    {
        Console.WriteLine($"Key {args.ChangedKey} changed value to {args.NewValue}");
        mostRecentChange = args;
        onKeyChange.TrySetResult();
        return Task.CompletedTask;
    }

    // subscribe to the key change event
    stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChange;

    // observe for notifications when the key changes values
    await stateStoreClient.ObserveAsync(stateStoreKey);

    await SetKeyAndWaitForNotification(stateStoreClient, stateStoreKey, stateStoreValue, onKeyChange);
    await SetKeyAndWaitForNotification(stateStoreClient, stateStoreKey, newValue, onKeyChange);

    await UnobserveKey(stateStoreClient, stateStoreKey, stateStoreValue, onKeyChange);
}
catch(Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("The End.");

async Task SetKeyAndWaitForNotification(StateStoreClient client, string key, string value, TaskCompletionSource tcs)
{
    Console.WriteLine($"Setting the key to {value}...");
    await client.SetAsync(key, value);
    await tcs.Task;
    tcs = new TaskCompletionSource();
}

async Task UnobserveKey(StateStoreClient client, string key, string value, TaskCompletionSource tcs)
{
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    cts.Token.Register(() => tcs.TrySetResult());

    Console.WriteLine("Unobserving the key, setting/deleting the key should not be successfully observed...");
    await client.UnobserveAsync(key);

    Console.WriteLine($"Setting the key to {value}...");
    await client.SetAsync(key, value);
    Console.WriteLine("Deleting the key...");
    await client.DeleteAsync(key);

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Console.WriteLine("Successfully unobserved the key.");
}