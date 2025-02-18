// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.StateStore;
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
    StateStoreSetResponse setResponse =
        await stateStoreClient.SetAsync(stateStoreKey, stateStoreValue);

    if (setResponse.Success)
    {
        Console.WriteLine($"Successfully set key {stateStoreKey} with value {stateStoreValue}");
    }
    else
    {
        Console.WriteLine($"Failed to set key {stateStoreKey} with value {stateStoreValue}");
    }

    StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(stateStoreKey!);

    if (getResponse.Value != null)
    {
        Console.WriteLine($"Current value of key {stateStoreKey} in the state store is {getResponse.Value.GetString()}");
    }
    else
    {
        Console.WriteLine($"The key {stateStoreKey} is not currently in the state store");
    }

    StateStoreDeleteResponse deleteResponse = await stateStoreClient.DeleteAsync(stateStoreKey!);

    if (deleteResponse.DeletedItemsCount == 1)
    {
        Console.WriteLine($"Successfully deleted key {stateStoreKey} from the state store");
    }
    else
    {
        Console.WriteLine($"Failed to delete key {stateStoreKey} from the state store");
    }
}
finally
{
    await stateStoreClient.DisposeAsync(true);
}
Console.WriteLine("The End");
