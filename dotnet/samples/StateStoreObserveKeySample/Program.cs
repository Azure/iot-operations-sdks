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

    // change the key value
    Console.WriteLine("Setting the key...");
    await stateStoreClient.SetAsync(stateStoreKey, stateStoreValue);

    // wait for the key change notification
    await onKeyChange.Task;

    // change the key value again
    Console.WriteLine("Setting the key again...");
    await stateStoreClient.SetAsync(stateStoreKey, newValue);

    // wait for the key change notification
    await onKeyChange.Task;

    // create a CTS with a timeout
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

    // mark the TCS as complete if the timeout is reached
    cts.Token.Register(() => onKeyChange.TrySetResult());

    // unobserve the key
    await stateStoreClient.UnobserveAsync(stateStoreKey);

    // update and delete key to ensure neither triggers OnKeyChange
    Console.WriteLine("Setting the key to a new value...");
    await stateStoreClient.SetAsync(stateStoreKey, stateStoreValue);
    Console.WriteLine("Deleting the key...");
    await stateStoreClient.DeleteAsync(stateStoreKey);

    // wait for the key change notification
    await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Console.WriteLine("Successfully unobserved the key.");
}
catch(TimeoutException)
{
    Console.WriteLine("Timed out waiting for key change notification.");
    throw;
}

Console.WriteLine("The End.");