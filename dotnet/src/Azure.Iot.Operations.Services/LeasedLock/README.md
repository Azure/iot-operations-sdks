# Leased Lock Client

This folder contains the source code for the IoT MQ .NET Leased Lock Client.

## Concept

The leased lock client for IoT MQ is built upon MQ's distributed state store and allows a user to 
access/edit shared resources in the state store without risk of race conditions.

Users provide a name of the lock to use, then one to many processes can try to acquire that lock. 
If a client's attempt to acquire the lock is successful, then the client will be granted a fencing 
token. The client must provide this fencing token when updating or deleting a shared resource to 
ensure that the client is still the lock owner at the time of the update/delete.

For more details on this concept, see [this section](#additional-concept-details).

## Features

Users can use the leased lock client to make a single attempt to acquire the lock

```csharp
// This call will return after the first attempt at acquiring the lock. The returned value
// will tell you if this client successfully acquired the lock or not.
AcquireLockResponse response = 
    await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken);

if (response.Success)
{
    // access/delete/update some shared resource in the state store
}
```

Users can use the leased lock client to try to acquire the lock until it is acquired or the process is interrupted

```csharp
// This call will either return when the lock has been acquired or will throw
// OperationCancelledException if the provided cancellation token is cancelled beforehand.
AcquireLockResponse response =
    await leasedLockClient.AcquireLockAsync(TimeSpan.FromSeconds(1), null, cancellationToken);
```

Users can configure the leased lock client to automatically renew its position as the lock's owner

```csharp
leasedLockClient.AutomaticRenewalOptions = new LeasedLockAutomaticRenewalOptions()
{
    AutomaticRenewal = true,
    LeaseTermLength = _leaseTerm,
    RenewalPeriod = _leaseTerm.Subtract(TimeSpan.FromMilliseconds(10)),
};

await leasedLockClient.AcquireLockAsync();
```

Users can release the lock when they are done accessing/editing the shared resource

```csharp
ReleaseLockResponse response = await leasedLockClient.ReleaseLockAsync(null, cancellationToken);

if (response.Success)
{
    // Successfully released the lock
}
```

Users can check who the current lock owner is

```csharp
GetLockHolderResponse response = 
    await leasedLockClient.GetLockHolderAsync(null, cancellationToken);
Console.WriteLine("The current lock holder is " + response.LockHolder);
```

And users can passively monitor lock state changes

```csharp
leasedLockClient.LockChangeEventReceivedAsync += (sender, args) =>
{
    if (args.NewState == LockState.Acquired)
    {
        Console.WriteLine(args.NewLockHolder + " just acquired the lock.");
    }
    else if (args.NewState == LockState.Released)
    {
        Console.WriteLine(args.PreviousLockHolder + " just released the lock and now there is no owner.");
    }
};

await leasedLockClient.ObserveLockAsync();
```

## Appendix

### Additional concept details

#### Lock lease term length

Note that, when acquiring a lock, a leased lock client must specify how long it wants to own the 
lock for. This requirement is in place to prevent a lock from being acquired for an indefinite 
amount of time only for the owner's process to crash or stall. If this were to happen, then no one 
would be able to make changes to the shared resource. By requiring a lease term limit, a different 
client can acquire the lock as soon as the previous lease term finishes and changes can be made to 
the shared resource again. In general, shorter lease term limits make it so that passive  
replication scenarios are more responsive to lock owners crashing, but also require more network 
traffic. 

#### A client acquired the lock but failed to update the shared resource?

Even if a client acquires the lock, there are some scenarios where it could fail to update a shared
resource as expected.

 - If a client releases a lock and then tries to update a shared resource, it may fail because another client acquired the lock already.
 - If a client owned the lock, but the lease term expires, and then tries to update a shared resource, it may fail because another client acquired the lock already.

In both cases, clients are expected to re-acquire the lock again before attempting to update
the shared resource again.

#### Common pitfalls

In applications with a large number of clients vying to acquire the lock at any given moment, it is 
possible to encounter a herding effect once the lock becomes available. To avoid
cases like this where a sudden flurry of requests to acquire the lock overwhelm the MQ broker,
it is generally recommended to add a short exponentional backoff time with jitter before attempting
to acquire the lock. A simplified version of this logic can be seen below:

```csharp
leasedLockClient.LockChangeEventReceivedAsync += (sender, args) =>
{
    if (args.NewState == LockState.Released)
    {
        // Wait a random amount of time before attempting to acquire the lock so that
        // not every process that wants the lock tries to acquire it at the same time
        await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(minDelay, maxDelay)));
        var result = await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromSeconds(1));
    
        // Check if lock was granted, then use the provided fencing token to update shared resources.
    }
};

await leasedLockClient.ObserveLockAsync();
```

#### Client misbehavior

Note that not providing a fencing token or providing an incorrect/fabricated fencing token when 
updating or deleting the shared resource negates any race condition protection that this client
provides. For more on this, see [this section](#what-arent-lock-owners-protected-from).

For additional details about fencing tokens and how the service uses them to provide race condition
protections, see [this document](https://github.com/Azure/iotedge-broker/blob/main/docs/state-store/concept-about-state-store-protocol.md#locking-and-fencing-tokens).


#### Scaling considerations

The leased lock algorithm used by this client and the MQ broker scales linearly because there is
no direct communication between potential lock acquirers. Each lock acquirer only needs to
communicate with the broker. There is no quorom-style agreement between each candidate like some 
other leased lock algorithms.

#### What aren't lock owners protected from?

Once a lock is acquired, a client is not protected from race conditions when editing a shared 
resource that lives outside of the distributed state store. A client is only protected when editing 
shared resources that live within the distributed state store and only if the latest granted
fencing token is provided when editing that shared resource. 

If you are interested in using this client to get race condition protections on data that lives
outside of the state store, you should reference [this document](https://github.com/Azure/iotedge-broker/blob/main/docs/state-store/concept-about-state-store-protocol.md#locking-and-fencing-tokens) which explains how MQ's state store uses fencing tokens to provide this protection.

An example of the correct usage of this leased lock client in conjunction with the state 
store client looks like the below snippet:

```csharp
AcquireLockResponse response = 
    await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken);

if (response.Success)
{
    StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
        "someSharedResourceKey", 
        "someNewValue",
        new StateStoreSetRequestOptions()
        {
            FencingToken = response.FencingToken,
        });

    if (setResponse.Success)
    {
        Console.WriteLine("Successfully acquired the lock and modified a shared resource");
    }
    else
    {
        Console.WriteLine("Successfully acquired the lock, but another client acquired it more recently");
    }
}
```