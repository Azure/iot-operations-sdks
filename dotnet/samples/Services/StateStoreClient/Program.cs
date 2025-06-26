﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;

var mqttClient = new MqttSessionClient();

MqttConnectionSettings connectionSettings = MqttConnectionSettings.FromEnvVars();
MqttClientConnectResult result = await mqttClient.ConnectAsync(connectionSettings);
await using ApplicationContext applicationContext = new ApplicationContext();
if (result.ResultCode != MqttClientConnectResultCode.Success)
{
    throw new Exception($"Failed to connect to MQTT broker. Code: {result.ResultCode} Reason: {result.ReasonString}");
}

StateStoreClient stateStoreClient = new(applicationContext, mqttClient);

try
{
    string stateStoreKey = "someKey";
    string stateStoreValue = "someValue";
    StateStoreSetRequestOptions setOptions = new()
    {
        // You can optionally persist state store entries so that the state store recovers them
        // upon a device restart
        PersistEntry = true
    };

    StateStoreSetResponse setResponse =
        await stateStoreClient.SetAsync(stateStoreKey, stateStoreValue, setOptions);

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
