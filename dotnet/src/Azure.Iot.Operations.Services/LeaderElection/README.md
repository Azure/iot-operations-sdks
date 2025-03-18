# Leader Election Client

This folder contains the source code for the IoT MQ .NET Leader Election Client.

## Concept

The leader election for IoT MQ is built upon MQ's distributed state store and allows a user to 
access/edit shared resources in the state store without risk of race conditions.

Users provide a name of the leadership position to use, then one to many processes can try to 
be elected leader of that position. If a client's campaign results in the client being elected 
the leader, then the client will be granted a fencing token. The client must provide this fencing 
token when updating or deleting a shared resource to ensure that the client is still the leader at 
the time of the update/delete.

For more details on this concept, see [this section](#additional-concept-details).

## Features

Users can use the leader election client to make a single attempt to campaign to be leader

```csharp
// This call will return after the first attempt at becoming leader. The returned value
// will tell you if this client was successfully elected or not.
CampaignResponse response = 
    await leaderElectionClient.TryCampaignAsync(TimeSpan.FromSeconds(1), cancellationToken);

if (response.IsLeader())
{
    // access/delete/update some shared resource in the state store
}
```

Users can use the leader election client to campaign to be leader until elected or interrupted

```csharp
// This call will either return when elected leader or will throw OperationCancelledException
// if the provided cancellation token is cancelled.
CampaignResponse response =
    await leaderElectionClient.CampaignAsync(TimeSpan.FromSeconds(1), null, cancellationToken);
```

Users can configure the leader election client to automatically renew its position as leader

```csharp
leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
{
    AutomaticRenewal = true,
    ElectionTerm = _electionTerm,
    RenewalPeriod = _electionTerm.Subtract(TimeSpan.FromMilliseconds(10)),
};

await leaderElectionClient.CampaignAsync();
```


Users can resign from being the leader when they are done accessing/editing the shared resource

```csharp
ResignationResponse response = await leaderElectionClient.ResignAsync(null, cancellationToken);

if (response.Success)
{
    // Successfully resigned from being the leader
}
```

Users can check who the current leader is

```csharp
GetCurrentLeaderResponse response = 
    await leaderElectionClient.GetCurrentLeaderAsync(null, cancellationToken);
Console.WriteLine("The current leader is " + response.CurrentLeader);
```

And users can passively monitor leadership changes

```csharp
leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
{
    if (args.NewState == LeadershipPositionState.LeaderElected)
    {
        Console.WriteLine("Leader " + args.NewLeader + " was just elected.");
    }
    else if (args.NewState == LeadershipPositionState.NoLeader)
    {
        Console.WriteLine("Leader " + args.PreviousLeader + " just resigned and now there is no leader.");
    }
};

await leaderElectionClient.ObserveLeadershipChangesAsync();
```

## Appendix

### Additional concept details

#### Election term length

Note that, when campaigning, a leader election client must specify how long it wants to be leader
for. This requirement is in place to prevent a leader from being elected for an indefinite amount
of time only for that leader's process to crash or stall. If this were to happen, then no one would 
be able to make changes to the shared resource. By requiring a term limit, a new leader can be 
elected as soon as the previous term finishes and changes can be made to the shared resource again.
In general, shorter term limits make it so that passive replication scenarios are more responsive 
to leaders crashing, but do require more network traffic. 

#### A client was elected leader but failed to update the shared resource?

Even if a client is elected leader, there are some scenarios where it could fail to update a shared
resource as expected.

 - If a client resigns from being a leader and then tries to update a shared resource, it may fail because another client was elected already.
 - If a client was the leader, but the term length expires, and then tries to update a shared resource, it may fail because another client was elected already.

In both cases, clients are expected to re-campaign to be leader again before attempting to update
the shared resource again.

#### Common pitfalls

In applications with a large number of clients vying to be a leader at any given moment, it is 
possible to encounter a herding effect once the leadership position becomes available. To avoid
cases like this where a sudden flurry of requests to become leader overwhelm the MQ broker,
it is generally recommended to add a short exponential backoff time with jitter before attempting
to campaign to be the leader. A simplified version of this logic can be seen below:

```csharp
leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
{
    if (args.NewState == LeadershipPositionState.NoLeader)
    {
        // Wait a random amount of time before attempting to campaign to be leader so that
        // not every candidate campaigns at the same time
        await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(minDelay, maxDelay)));
        var result = await leaderElectionClient.TryCampaignAsync(TimeSpan.FromSeconds(1));
    
        // check if elected, then use leadership position to update shared resources.
    }
};

await leaderElectionClient.ObserveLeadershipChangesAsync();
```

#### Client misbehavior

Not providing a fencing token or providing an incorrect/fabricated fencing token when 
updating or deleting the shared resource negates any race condition protection that this client
provides. For more on this, see [this section](#what-arent-leaders-protected-from).

For additional details about fencing tokens and how the service uses them to provide race condition
protections, see [this document](https://learn.microsoft.com/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol#locking-and-fencing-tokens).

#### Scaling considerations

The leader election algorithm used by this client and the MQ broker scales linearly because there is
no inter-candidate communication. Each leader election candidate only needs to communicate with 
the broker. There is no quorom-style agreement between each candidate like some other leader 
election algorithms.

#### What aren't leaders protected from?

Once elected leader, a client is not protected from race conditions when editing a shared resource 
that lives outside of the distributed state store. A client is only protected when editing shared 
resources that live within the distributed state store and only if the latest granted fencing token
is provided when editing that shared resource. 

If you are interested in using this client to get race condition protections on data that lives
outside of the state store, you should reference [this document](https://learn.microsoft.com/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol#locking-and-fencing-tokens) which explains how MQ's state store uses fencing tokens to provide this protection.

An example of the correct usage of this leader election client in conjunction with the state 
store client looks like the below snippet:

```csharp
CampaignResponse response = 
    await leaderElectionClient.TryCampaignAsync(TimeSpan.FromSeconds(1), cancellationToken);

if (response.IsLeader)
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
        Console.WriteLine("Successfully became leader and modified a shared resource");
    }
    else
    {
        Console.WriteLine("Successfully became leader, but another client was elected more recently");
    }
}
```

#### Relation to leased lock client

The leader election client is largely the same as the leased lock client other than some
naming conventions. Currently, the leader election client even uses a leased lock client
for all of its operations. There is no benefit to using one of these clients over the other.