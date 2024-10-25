using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using Xunit;

namespace Azure.Iot.Operations.Services.IntegrationTest;

public class LeaderElectionClientIntegrationTests
{
    [Fact]
    public async Task TestFencing()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        string candidateName = Guid.NewGuid().ToString();
        await using var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString(), candidateName);
        await using var stateStoreClient = new StateStoreClient(mqttClient);
        GetCurrentLeaderResponse getCurrentLeaderResponse =
            await leaderElectionClient.GetCurrentLeaderAsync();

        // Since the election for this lock was just created, and no other process is aware of it,
        // there shouldn't be a current leader.
        Assert.Null(getCurrentLeaderResponse.CurrentLeader);

        CampaignResponse campaignResponse =
            await leaderElectionClient.TryCampaignAsync(TimeSpan.FromMinutes(1));

        Assert.True(campaignResponse.IsLeader);

        getCurrentLeaderResponse =
            await leaderElectionClient.GetCurrentLeaderAsync();

        // Since this client was just elected leader, the current leader should be equal to this client's candidate name.
        Assert.NotNull(getCurrentLeaderResponse.CurrentLeader);
        Assert.Equal(candidateName, getCurrentLeaderResponse.CurrentLeader.GetString());

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = campaignResponse.FencingToken,
            });

        Assert.True(setResponse.Success);

        Assert.True((await leaderElectionClient.ResignAsync()).Success);

        getCurrentLeaderResponse =
            await leaderElectionClient.GetCurrentLeaderAsync();

        // Since this client was the leader and just resigned, and no other process is aware of this lock,
        // there should be no current leader.
        Assert.Null(getCurrentLeaderResponse.CurrentLeader);
    }

    [Fact]
    public async Task TestFencingWithCampaignAndUpdateValueAsync()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        string holderId = Guid.NewGuid().ToString();
        await using var stateStoreClient = new StateStoreClient(mqttClient);
        await using var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString(), holderId);

        StateStoreValue initialValue = "someInitialValue";
        StateStoreValue updatedValue = "someUpdatedValue";

        await stateStoreClient.SetAsync(sharedResourceName, initialValue);

        await leaderElectionClient.CampaignAndUpdateValueAsync(
            sharedResourceName,
            (currentValue) =>
            {
                if (currentValue != null && currentValue.Equals(initialValue))
                {
                    return updatedValue;
                }

                return initialValue;
            });

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(sharedResourceName);

        Assert.NotNull(getResponse.Value);
        Assert.Equal(updatedValue, getResponse.Value);
    }

    [Fact]
    public async Task TestFencingWithSessionId()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        await using var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString(), "someCandidate");
        await using var stateStoreClient = new StateStoreClient(mqttClient);

        var campaignOptions = new CampaignRequestOptions()
        {
            SessionId = Guid.NewGuid().ToString(),
        };

        CampaignResponse campaignResponse =
            await leaderElectionClient.TryCampaignAsync(TimeSpan.FromMinutes(1), campaignOptions);

        Assert.True(campaignResponse.IsLeader);

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = campaignResponse.FencingToken,
            });

        Assert.True(setResponse.Success);

        var resignationRequestOptions = new ResignationRequestOptions()
        {
            SessionId = campaignOptions.SessionId,
        };

        Assert.True((await leaderElectionClient.ResignAsync(resignationRequestOptions)).Success);
    }

    [Fact]
    public async Task TestProactivelyRecampaigning()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        var stateStoreClient = new StateStoreClient(mqttClient);
        var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString());

        CampaignResponse campaignResponse = await leaderElectionClient.TryCampaignAsync(TimeSpan.FromMinutes(1));

        Assert.True(campaignResponse.IsLeader);
        Assert.NotNull(campaignResponse.FencingToken);
        HybridLogicalClock firstFencingToken = campaignResponse.FencingToken;

        // Acquire the same lock again to check what the fencing token looks like.
        campaignResponse =
            await leaderElectionClient.TryCampaignAsync(
                TimeSpan.FromMinutes(1));

        Assert.True(campaignResponse.IsLeader);
        Assert.NotNull(campaignResponse.FencingToken);
        HybridLogicalClock secondFencingToken = campaignResponse.FencingToken;

        // The second fencing token should be "later" than the first one since the 
        // service is expected to "increment" the fencing token even if the request
        // was from a client that already owns the lock.
        Assert.NotEqual(firstFencingToken, secondFencingToken);
        Assert.True(secondFencingToken.CompareTo(firstFencingToken) > 0);

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = campaignResponse.FencingToken,
            });

        Assert.True(setResponse.Success);
    }

    [Fact]
    public async Task TestAutomaticRenewal()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        await using var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString(), "someCandidate");
        await using var stateStoreClient = new StateStoreClient(mqttClient);

        var electionTermLength = TimeSpan.FromSeconds(2);

        leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
        {
            AutomaticRenewal = true,
            ElectionTerm = electionTermLength,
            RenewalPeriod = TimeSpan.FromSeconds(1),
        };

        await leaderElectionClient.TryCampaignAsync(electionTermLength);

        Assert.NotNull(leaderElectionClient.LastKnownCampaignResult);
        Assert.True(leaderElectionClient.LastKnownCampaignResult.IsLeader);
        Assert.NotNull(leaderElectionClient.LastKnownCampaignResult.FencingToken);
        HybridLogicalClock firstFencingToken = leaderElectionClient.LastKnownCampaignResult.FencingToken;

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = leaderElectionClient.LastKnownCampaignResult.FencingToken,
            });

        Assert.True(setResponse.Success);

        // Wait a bit so that auto-renewal happens once or twice since initially being elected leader
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        HybridLogicalClock automaticallyRenewedFencingToken = leaderElectionClient.LastKnownCampaignResult.FencingToken;
        while (automaticallyRenewedFencingToken.CompareTo(firstFencingToken) == 0)
        {
            automaticallyRenewedFencingToken = leaderElectionClient.LastKnownCampaignResult.FencingToken;
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }

        setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = leaderElectionClient.LastKnownCampaignResult.FencingToken,
            });

        // The operation on a shared resource should still succeed because the leader election client
        // kept re-campaigning to be leader (and no other client was campaigning on the same lock)
        Assert.True(setResponse.Success);

        var resignationRequestOptions = new ResignationRequestOptions()
        {
            CancelAutomaticRenewal = true,
        };

        Assert.True((await leaderElectionClient.ResignAsync(resignationRequestOptions)).Success);

        // Wait a bit before checking the final fencing token to ensure that
        // no automatic renewal was happening when resigning
        await Task.Delay(TimeSpan.FromSeconds(3));

        automaticallyRenewedFencingToken = leaderElectionClient.LastKnownCampaignResult.FencingToken;

        // Wait a bit so that auto-renewal would happen once or twice if disabling it failed
        await Task.Delay(TimeSpan.FromSeconds(5));

        // The most recent fencing token should be equal to the final fencing token saved before disabling auto-renewal
        Assert.Equal(0, automaticallyRenewedFencingToken.CompareTo(leaderElectionClient.LastKnownCampaignResult.FencingToken));
    }

    [Fact]
    public async Task TestObserveLeadershipChangesCallback()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        string candidateName = Guid.NewGuid().ToString();
        await using var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString(), candidateName);
        var onCallbackExecuted = new TaskCompletionSource<LeadershipChangeEventArgs>();
        leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
        {
            onCallbackExecuted.SetResult(args);
            return Task.CompletedTask;
        };

        await leaderElectionClient.ObserveLeadershipChangesAsync();

        CampaignResponse campaignResponse =
            await leaderElectionClient.TryCampaignAsync(
                TimeSpan.FromMinutes(1));

        Assert.True(campaignResponse.IsLeader);

        LeadershipChangeEventArgs? eventArgs = null;

        try
        {
            eventArgs = await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.NotNull(eventArgs);
        Assert.Equal(LeadershipPositionState.LeaderElected, eventArgs.NewState);
        Assert.NotNull(eventArgs.NewLeader);
        Assert.Equal(candidateName, eventArgs.NewLeader.GetString());
        Assert.NotNull(eventArgs.Timestamp);

        // Set a new TCS so that we can monitor the next callback as well
        onCallbackExecuted = new TaskCompletionSource<LeadershipChangeEventArgs>();

        Assert.True((await leaderElectionClient.ResignAsync()).Success);

        try
        {
            eventArgs = await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.Equal(LeadershipPositionState.NoLeader, eventArgs.NewState);
        Assert.Null(eventArgs.NewLeader);

        // Unobserve the leadership poisition, then campaign again to check that the 
        // callback doesn't execute anymore
        await leaderElectionClient.UnobserveLeadershipChangesAsync();

        // Set a new TCS so that we can monitor the next callback as well
        onCallbackExecuted = new TaskCompletionSource<LeadershipChangeEventArgs>();

        campaignResponse =
            await leaderElectionClient.TryCampaignAsync(
                TimeSpan.FromMilliseconds(100));

        Assert.True(campaignResponse.IsLeader);

        // Wait a bit before checking if the callback was executed again
        await Task.Delay(TimeSpan.FromSeconds(3));

        // The callback should no longer execute since this client unobserved the leadership position
        Assert.False(onCallbackExecuted.Task.IsCompleted);
    }

    [Fact]
    public async Task TestUnobserveLeadershipChangesCallback()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        var sharedResourceName = Guid.NewGuid().ToString();

        string candidateName = Guid.NewGuid().ToString();
        var leaderElectionClient = new LeaderElectionClient(mqttClient, Guid.NewGuid().ToString(), candidateName);
        var onCallbackExecuted = new TaskCompletionSource<LeadershipChangeEventArgs>();
        leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
        {
            onCallbackExecuted.SetResult(args);
            return Task.CompletedTask;
        };

        await leaderElectionClient.ObserveLeadershipChangesAsync();

        CampaignResponse campaignResponse =
            await leaderElectionClient.TryCampaignAsync(
                TimeSpan.FromMinutes(1));

        Assert.True(campaignResponse.IsLeader);

        LeadershipChangeEventArgs? eventArgs = null;

        try
        {
            eventArgs = await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.NotNull(eventArgs);
        Assert.Equal(LeadershipPositionState.LeaderElected, eventArgs.NewState);
        Assert.NotNull(eventArgs.NewLeader);
        Assert.Equal(candidateName, eventArgs.NewLeader.GetString());

        // Set a new TCS so that we can monitor the next callback as well
        onCallbackExecuted = new TaskCompletionSource<LeadershipChangeEventArgs>();

        // Unobserve the leadership poisition, then campaign again to check that the 
        // callback doesn't execute anymore
        await leaderElectionClient.UnobserveLeadershipChangesAsync();

        // Campaign and then resign to ensure that neither of these events can still trigger
        // a callback after unobserving this leadership position.
        await leaderElectionClient.ResignAsync();
        await leaderElectionClient.TryCampaignAsync(TimeSpan.FromMilliseconds(100));

        try
        {
            await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Fail("Expected the callback to not execute now that the client isn't observing this leadership position");
        }
        catch (TimeoutException)
        {
            // Expected result since the callback should not execute after unobserving the leadership position.
        }
    }

    [Fact]
    public async Task TestCampaignWhenLeadershipPositionIsUnavailable()
    {
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");

        string leadershipPositionId = Guid.NewGuid().ToString();
        await using var leaderElectionClient1 = new LeaderElectionClient(mqttClient1, leadershipPositionId, Guid.NewGuid().ToString());
        await using var leaderElectionClient2 = new LeaderElectionClient(mqttClient2, leadershipPositionId, Guid.NewGuid().ToString());

        // Make leaderElectionClient1 release the lock after a few seconds
        CampaignResponse response1 =
            await leaderElectionClient1.TryCampaignAsync(TimeSpan.FromSeconds(5));

        Assert.True(response1.IsLeader);

        // This client cannot acquire the lock right away since leaderElectionClient1 holds it for another few seconds,
        // but it should still acquire the lock after leaderElectionClient1's term ends.
        CampaignResponse response2 =
            await leaderElectionClient2.CampaignAsync(TimeSpan.FromSeconds(1), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

        Assert.True(response2.IsLeader);
    }

    [Fact]
    public async Task TestAcquireLockAndUpdateValueAsyncWhenLockIsUnavailable()
    {
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");

        var sharedResourceName = Guid.NewGuid().ToString();
        string lockId = Guid.NewGuid().ToString();
        await using var leaderElectionClient1 = new LeaderElectionClient(mqttClient1, lockId, Guid.NewGuid().ToString());
        await using var leaderElectionClient2 = new LeaderElectionClient(mqttClient2, lockId, Guid.NewGuid().ToString());

        await using var stateStoreClient = new StateStoreClient(mqttClient1);

        // Make leaderElectionClient1 release the lock after a few seconds
        CampaignResponse response1 =
            await leaderElectionClient1.TryCampaignAsync(TimeSpan.FromSeconds(5));

        Assert.True(response1.IsLeader);

        // Make LeaderElectionClient2 attempt to be leader and update the shared
        // value while the LeaderElectionClient1 is still the leader. This function should block
        // until the leadership position is available.
        StateStoreValue updatedValue = "someUpdatedValue";
        await leaderElectionClient2.CampaignAndUpdateValueAsync(
            sharedResourceName,
            (currentValue) =>
            {
                return updatedValue;
            });

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(sharedResourceName);

        Assert.NotNull(getResponse.Value);
        Assert.Equal(updatedValue, getResponse.Value);
    }

    [Fact]
    public async Task TestCampaignAndUpdateValueAsyncDoesNotUpdateValueIfLockNotAcquired()
    {
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");

        var sharedResourceName = Guid.NewGuid().ToString();
        var sharedResourceInitialValue = Guid.NewGuid().ToString();

        string lockId = Guid.NewGuid().ToString();
        await using var leaderElectionClient1 = new LeaderElectionClient(mqttClient1, lockId, Guid.NewGuid().ToString());
        await using var leaderElectionClient2 = new LeaderElectionClient(mqttClient2, lockId, Guid.NewGuid().ToString());

        await using var stateStoreClient = new StateStoreClient(mqttClient1);

        // Make leaderElectionClient1 hold the lock during the entire test
        CampaignResponse response1 =
            await leaderElectionClient1.TryCampaignAsync(TimeSpan.FromHours(10));

        Assert.True(response1.IsLeader);

        await stateStoreClient.SetAsync(sharedResourceName, sharedResourceInitialValue);

        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // The client will attempt to campaign, but the leadership position won't be available
        // before the provided cancellation token requests cancellation. As a result,
        // the value of the shared resource should not be updated.
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await leaderElectionClient2.CampaignAndUpdateValueAsync(
                sharedResourceName,
                (currentValue) =>
                {
                    return "someUpdatedValue";
                },
                cancellationToken: cts.Token));

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(sharedResourceName);

        // Because the call to CampaignAndUpdateValueAsync was never elected leader, the value
        // of the shared resource should still be equal to the initial value.
        Assert.NotNull(getResponse.Value);
        Assert.Equal(sharedResourceInitialValue, getResponse.Value);

    }

}