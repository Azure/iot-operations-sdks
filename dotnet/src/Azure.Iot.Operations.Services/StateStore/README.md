# State Store Client

This folder contains the source code for the IoT MQ .NET State Store Client.

## Concept

IoT MQ deployments come with a [distributed state store](https://github.com/Azure/iotedge-broker/blob/main/docs/state-store/concept-about-state-store-protocol.md#azure-iot-mq-state-store-protocol)
and this client allows you to interact with that state store over an MQTT connection.


## Features

This client allows you to get, set and delete keys from the state store.

```csharp
StateStoreGetResponse getResponse = await stateStoreClient.GetAsync("myKey");
await stateStoreClient.SetAsync("myKey", "someNewValue");
await stateStoreClient.DeleteAsync("myKey");
```

This client also allows you to subscribe to receive notifications whenever a particular key in 
the state store is changed.

```csharp
stateStoreClient.KeyChangeMessageReceivedAsync += (sender, args) =>
{
    if (args.NewState == KeyState.Deleted)
    {
        Console.WriteLine("Key " + args.ChangedKey + " was deleted");
    }
    else if (args.NewState == KeyState.Updated)
    {
        Console.WriteLine("Key " + args.ChangedKey + " now has value " + args.NewValue);
    }
};

await stateStoreClient.ObserveAsync("myKey");
```

The state store (and this client) allow you to pass in keys and/or values as arbitrary binary as well

```csharp
await stateStoreClient.SetAsync("myKeyWithBinaryValue", new StateStoreValue(new byte[10]));
await stateStoreClient.SetAsync("myKeyWithStringValue", "someStringValue");
await stateStoreClient.SetAsync(new StateStoreKey(new byte[10]), "myValueWithBinaryKey");
await stateStoreClient.SetAsync("someStringValue", "myValueWithStringKey");
```

This client allows you to get the version of a particular key as well

```csharp
StateStoreGetResponse getResponse = await stateStoreClient.GetAsync("myKey");

if (myValue != null)
{
    Console.WriteLine("Current value: " + getResponse.Value);
    Console.WriteLine("Current version: " + getResponse.Version);
}
```

Finally, the state store client can be used in conjunction with either the [leader election client](../LeaderElection/LeaderElectionClient.cs) 
or the [leased lock client](../LeasedLock/LeasedLockClient.cs) to edit shared resources in the state
store while being protected from race conditions. For details on this point, see the 
[leader election readme](../LeaderElection/README.md#what-arent-leaders-protected-from) 
and/or the [leased lock readme](../LeasedLock/README.md#what-arent-lock-owners-protected-from). 
Additionally, see [this document](https://github.com/Azure/iotedge-broker/blob/main/docs/state-store/concept-about-state-store-protocol.md#locking-and-fencing-tokens) for more details on how the state store itself uses versioning and 
fencing tokens to provide this protection from race conditions.
