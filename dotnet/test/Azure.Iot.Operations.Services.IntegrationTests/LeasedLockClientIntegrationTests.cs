// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.LeasedLock;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using Xunit;

namespace Azure.Iot.Operations.Services.IntegrationTest;

public class LeasedLockClientIntegrationTests
{
    [Fact]
    public async Task TestFencing()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        string holderId = Guid.NewGuid().ToString();
        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient);
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString(), holderId);

        GetLockHolderResponse getLockHolderResponse = await leasedLockClient.GetLockHolderAsync();

        // Since the lock was just created, and no other process is aware of this lock,
        // there should be no lock holder.
        Assert.Null(getLockHolderResponse.LockHolder);

        AcquireLockResponse acquireLockResponse =
            await leasedLockClient.TryAcquireLockAsync(
                TimeSpan.FromMinutes(10));

        Assert.True(acquireLockResponse.Success);

        getLockHolderResponse =
            await leasedLockClient.GetLockHolderAsync();

        // Since the lock was just acquired the lock holder should be equal to this client's holderId.
        Assert.NotNull(getLockHolderResponse.LockHolder);
        Assert.Equal(holderId, getLockHolderResponse.LockHolder.GetString());

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = acquireLockResponse.FencingToken,
            });

        Assert.True(setResponse.Success);

        // Attempt another set operation with an incorrect fencing token, expect it to fail
        HybridLogicalClock incorrectFencingToken = new HybridLogicalClock()
        {
            Timestamp = DateTime.UnixEpoch
        };

        // The service should reject the request
        await Assert.ThrowsAsync<StateStoreOperationException>(
            async () => await stateStoreClient.SetAsync(
                sharedResourceName,
                Guid.NewGuid().ToString(),
                new StateStoreSetRequestOptions()
                {
                    FencingToken = incorrectFencingToken,
                }));

        // The service should reject the request
        await Assert.ThrowsAsync<StateStoreOperationException>(
            async () => await stateStoreClient.DeleteAsync(
                sharedResourceName,
                new StateStoreDeleteRequestOptions()
                {
                    FencingToken = incorrectFencingToken,
                }));

        ReleaseLockResponse releaseLockResponse =
            await leasedLockClient.ReleaseLockAsync();

        Assert.True(releaseLockResponse.Success);

        getLockHolderResponse =
            await leasedLockClient.GetLockHolderAsync();

        // Since the lock was just released, and no other process is aware of this lock,
        // there should be no lock holder.
        Assert.Null(getLockHolderResponse.LockHolder);
    }

    [Fact]
    public async Task TestFencingWithAcquireLockAndUpdateValueAsync()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        string holderId = Guid.NewGuid().ToString();
        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient);
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString(), holderId);

        StateStoreValue initialValue = "someInitialValue";
        StateStoreValue updatedValue = "someUpdatedValue";

        await stateStoreClient.SetAsync(sharedResourceName, initialValue);

        await leasedLockClient.AcquireLockAndUpdateValueAsync(
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
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient);
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString());

        var acquireLockOptions = new AcquireLockRequestOptions()
        {
            SessionId = Guid.NewGuid().ToString(),
        };

        AcquireLockResponse acquireLockResponse =
            await leasedLockClient.TryAcquireLockAsync(
                TimeSpan.FromMinutes(10),
                acquireLockOptions);

        Assert.True(acquireLockResponse.Success);

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = acquireLockResponse.FencingToken,
            });

        Assert.True(setResponse.Success);

        // Attempt another set operation with an incorrect fencing token, expect it to fail
        HybridLogicalClock incorrectFencingToken = new HybridLogicalClock()
        {
            Timestamp = DateTime.UnixEpoch
        };

        // The service should reject the request
        await Assert.ThrowsAsync<StateStoreOperationException>(
            async () => await stateStoreClient.SetAsync(
                sharedResourceName,
                Guid.NewGuid().ToString(),
                new StateStoreSetRequestOptions()
                {
                    FencingToken = incorrectFencingToken,
                }))
            ;

        var releaseLockOptions = new ReleaseLockRequestOptions()
        {
            SessionId = acquireLockOptions.SessionId,
        };

        ReleaseLockResponse releaseLockResponse =
            await leasedLockClient.ReleaseLockAsync(releaseLockOptions);

        Assert.True(releaseLockResponse.Success);
    }

    [Fact]
    public async Task TestProactivelyReacquiringALock()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient);
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString());

        AcquireLockResponse acquireLockResponse = await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromMinutes(10));

        Assert.True(acquireLockResponse.Success);
        Assert.NotNull(acquireLockResponse.FencingToken);
        HybridLogicalClock firstFencingToken = acquireLockResponse.FencingToken;

        // Acquire the same lock again to check what the fencing token looks like.
        acquireLockResponse =
            await leasedLockClient.TryAcquireLockAsync(
                TimeSpan.FromMinutes(10));

        Assert.True(acquireLockResponse.Success);
        Assert.NotNull(acquireLockResponse.FencingToken);
        HybridLogicalClock secondFencingToken = acquireLockResponse.FencingToken;

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
                FencingToken = acquireLockResponse.FencingToken,
            });

        Assert.True(setResponse.Success);
    }

    [Fact]
    public async Task TestAutomaticRenewal()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString());
        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient);

        var leaseLength = TimeSpan.FromSeconds(2);

        leasedLockClient.AutomaticRenewalOptions = new LeasedLockAutomaticRenewalOptions()
        {
            AutomaticRenewal = true,
            LeaseTermLength = leaseLength,
            RenewalPeriod = TimeSpan.FromSeconds(1),
        };

        await leasedLockClient.TryAcquireLockAsync(leaseLength);

        Assert.NotNull(leasedLockClient.MostRecentAcquireLockResponse);
        Assert.True(leasedLockClient.MostRecentAcquireLockResponse.Success);
        Assert.NotNull(leasedLockClient.MostRecentAcquireLockResponse.FencingToken);
        HybridLogicalClock firstFencingToken = leasedLockClient.MostRecentAcquireLockResponse.FencingToken;

        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = leasedLockClient.MostRecentAcquireLockResponse.FencingToken,
            });

        Assert.True(setResponse.Success);

        // Wait a bit so that auto-renewal happens once or twice since initially acquiring the lock
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        HybridLogicalClock automaticallyRenewedFencingToken = leasedLockClient.MostRecentAcquireLockResponse.FencingToken;
        while (automaticallyRenewedFencingToken.CompareTo(firstFencingToken) == 0)
        {
            automaticallyRenewedFencingToken = leasedLockClient.MostRecentAcquireLockResponse.FencingToken;
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }

        setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = leasedLockClient.MostRecentAcquireLockResponse.FencingToken,
            });

        // The operation on a shared resource should still succeed because the leased lock client
        // kept re-acquiring the lock (and no other client was trying to acquire the same lock)
        Assert.True(setResponse.Success);

        var releaseLockRequestOptions = new ReleaseLockRequestOptions()
        {
            CancelAutomaticRenewal = true,
        };

        ReleaseLockResponse releaseLockResponse =
            await leasedLockClient.ReleaseLockAsync(releaseLockRequestOptions);

        Assert.True(releaseLockResponse.Success);

        // Wait a bit before checking the final fencing token to ensure that
        // no automatic renewal was happening when releasing the lock
        await Task.Delay(TimeSpan.FromSeconds(3));

        automaticallyRenewedFencingToken = leasedLockClient.MostRecentAcquireLockResponse.FencingToken;

        // Wait a bit so that auto-renewal would happen once or twice if disabling it failed
        await Task.Delay(TimeSpan.FromSeconds(5));

        // The most recent fencing token should be equal to the final fencing token saved before disabling auto-renewal
        Assert.Equal(0, automaticallyRenewedFencingToken.CompareTo(leasedLockClient.MostRecentAcquireLockResponse.FencingToken));
    }

    [Fact]
    public async Task TestObserveLockChangedCallback()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        string holderId = Guid.NewGuid().ToString();
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString(), holderId);

        var onCallbackExecuted = new TaskCompletionSource<LockChangeEventArgs>();
        leasedLockClient.LockChangeEventReceivedAsync += (sender, args) =>
        {
            onCallbackExecuted.SetResult(args);
            return Task.CompletedTask;
        };

        await leasedLockClient.ObserveLockAsync();

        AcquireLockResponse acquireLockResponse =
            await leasedLockClient.TryAcquireLockAsync(
                TimeSpan.FromMinutes(1));

        Assert.True(acquireLockResponse.Success);

        LockChangeEventArgs? eventArgs = null;

        try
        {
            eventArgs = await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.NotNull(eventArgs);
        Assert.Equal(LockState.Acquired, eventArgs.NewState);
        Assert.NotNull(eventArgs.NewLockHolder);
        Assert.Equal(holderId, eventArgs.NewLockHolder.GetString());
        Assert.NotNull(eventArgs.Timestamp);

        // Set a new TCS so that we can monitor the next callback as well
        onCallbackExecuted = new TaskCompletionSource<LockChangeEventArgs>();

        ReleaseLockResponse releaseLockResponse =
            await leasedLockClient.ReleaseLockAsync();

        Assert.True(releaseLockResponse.Success);

        try
        {
            eventArgs = await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.Equal(LockState.Released, eventArgs.NewState);
        Assert.Null(eventArgs.NewLockHolder);

        // Set a new TCS so that we can monitor the next callback as well
        onCallbackExecuted = new TaskCompletionSource<LockChangeEventArgs>();

        // Unobserve the leadership poisition, then campaign again to check that the 
        // callback doesn't execute anymore
        await leasedLockClient.UnobserveLockAsync();

        acquireLockResponse =
            await leasedLockClient.TryAcquireLockAsync(
                TimeSpan.FromMilliseconds(100));

        Assert.True(acquireLockResponse.Success);

        // Wait a bit before checking if the callback was executed again
        await Task.Delay(TimeSpan.FromSeconds(3));

        // The callback should no longer execute since this client unobserved the lock
        Assert.False(onCallbackExecuted.Task.IsCompleted);
    }

    [Fact]
    public async Task TestUnobserveLockChangedCallback()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        string holderId = Guid.NewGuid().ToString();
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString(), holderId);

        var onCallbackExecuted = new TaskCompletionSource<LockChangeEventArgs>();
        leasedLockClient.LockChangeEventReceivedAsync += (sender, args) =>
        {
            onCallbackExecuted.SetResult(args);
            return Task.CompletedTask;
        };

        await leasedLockClient.ObserveLockAsync();

        AcquireLockResponse acquireLockResponse =
            await leasedLockClient.TryAcquireLockAsync(
                TimeSpan.FromMinutes(1));

        Assert.True(acquireLockResponse.Success);

        LockChangeEventArgs? eventArgs = null;

        try
        {
            eventArgs = await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.NotNull(eventArgs);
        Assert.Equal(LockState.Acquired, eventArgs.NewState);
        Assert.NotNull(eventArgs.NewLockHolder);
        Assert.Equal(holderId, eventArgs.NewLockHolder.GetString());

        await leasedLockClient.UnobserveLockAsync();

        // Set a new TCS so that we can monitor the next callback as well
        onCallbackExecuted = new TaskCompletionSource<LockChangeEventArgs>();

        // Update and then delete the previously observed lock to ensure that neither of these events can still trigger
        // a callback after unobserving this lock.
        await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromMilliseconds(100));
        await leasedLockClient.ReleaseLockAsync();

        try
        {
            await onCallbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Fail("Expected the callback to not execute now that the client isn't observing this lock");
        }
        catch (TimeoutException)
        {
            // Expected result since the callback should not execute after unobserving the lock that was changed.
        }
    }

    [Fact]
    public async Task TestAcquireLockWhenLockIsUnavailable()
    {
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();

        string lockId = Guid.NewGuid().ToString();
        await using var leasedLockClient1 = new LeasedLockClient(applicationContext, mqttClient1, lockId, Guid.NewGuid().ToString());
        await using var leasedLockClient2 = new LeasedLockClient(applicationContext, mqttClient2, lockId, Guid.NewGuid().ToString());

        // Make leasedLockClient1 release the lock after a few seconds
        AcquireLockResponse response1 =
            await leasedLockClient1.TryAcquireLockAsync(TimeSpan.FromSeconds(5));

        Assert.True(response1.Success);

        // This client cannot acquire the lock right away since leasedLockClient1 holds it for another few seconds,
        // but it should still acquire the lock after leasedLockClient1's term ends.
        AcquireLockResponse response2 =
            await leasedLockClient2.AcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

        Assert.True(response2.Success);
    }

    [Fact]
    public async Task TestAcquireLockAndUpdateValueAsyncWhenLockIsUnavailable()
    {
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();

        var sharedResourceName = Guid.NewGuid().ToString();
        string lockId = Guid.NewGuid().ToString();
        await using var leasedLockClient1 = new LeasedLockClient(applicationContext, mqttClient1, lockId, Guid.NewGuid().ToString());
        await using var leasedLockClient2 = new LeasedLockClient(applicationContext, mqttClient2, lockId, Guid.NewGuid().ToString());

        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient1);

        // Make leasedLockClient1 release the lock after a few seconds
        AcquireLockResponse response1 =
            await leasedLockClient1.TryAcquireLockAsync(TimeSpan.FromSeconds(5));

        Assert.True(response1.Success);

        // Make LeasedLockClient2 attempt to grab the lock and update the shared
        // value while the LeasedLockClient1 still owns the lock. This function should block
        // until the lock is available.
        StateStoreValue updatedValue = "someUpdatedValue";
        await leasedLockClient2.AcquireLockAndUpdateValueAsync(
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
    public async Task TestAcquireLockAndUpdateValueAsyncDoesNotUpdateValueIfLockNotAcquired()
    {
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();

        var sharedResourceName = Guid.NewGuid().ToString();
        var sharedResourceInitialValue = Guid.NewGuid().ToString();

        string lockId = Guid.NewGuid().ToString();
        await using var leasedLockClient1 = new LeasedLockClient(applicationContext, mqttClient1, lockId, Guid.NewGuid().ToString());
        await using var leasedLockClient2 = new LeasedLockClient(applicationContext, mqttClient2, lockId, Guid.NewGuid().ToString());

        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient1);

        // Make leasedLockClient1 hold the lock during the entire test
        AcquireLockResponse response1 =
            await leasedLockClient1.TryAcquireLockAsync(TimeSpan.FromHours(10));

        Assert.True(response1.Success);

        await stateStoreClient.SetAsync(sharedResourceName, sharedResourceInitialValue);

        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // The client will attempt to acquire the lock, but it won't be made available
        // before the provided cancellation token requests cancellation. As a result,
        // the value of the shared resource should not be updated.
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await leasedLockClient2.AcquireLockAndUpdateValueAsync(
                sharedResourceName,
                (currentValue) =>
                {
                    return "someUpdatedValue";
                },
                cancellationToken: cts.Token));

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(sharedResourceName);

        // Because the call to AcquireLockAndUpdateValueAsync never acquired the lock, the value
        // of the shared resource should still be equal to the initial value.
        Assert.NotNull(getResponse.Value);
        Assert.Equal(sharedResourceInitialValue, getResponse.Value);
    }

    [Fact]
    public async Task TestFencingTokenLowerVersion()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new ApplicationContext();
        var sharedResourceName = Guid.NewGuid().ToString();

        string holderId = Guid.NewGuid().ToString();
        await using var stateStoreClient = new StateStoreClient(applicationContext, mqttClient);
        await using var leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, Guid.NewGuid().ToString(), holderId);

        AcquireLockResponse acquireLockResponse = await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromMinutes(10));
        Assert.True(acquireLockResponse.Success);

        // set a value in the state store with the fencing token
        StateStoreSetResponse setResponse = await stateStoreClient.SetAsync(
            sharedResourceName,
            Guid.NewGuid().ToString(),
            new StateStoreSetRequestOptions()
            {
                FencingToken = acquireLockResponse.FencingToken,
            });
        Assert.True(setResponse.Success);

        // create a lower version of the fencing token
        HybridLogicalClock lowerVersionFencingToken = new HybridLogicalClock()
        {
            Timestamp = DateTime.UnixEpoch
        };

        // attempt to set a value using the lower version fencing token (it should fail)
        var setException = await Assert.ThrowsAsync<StateStoreOperationException>(
            async () => await stateStoreClient.SetAsync(
                sharedResourceName,
                Guid.NewGuid().ToString(),
                new StateStoreSetRequestOptions()
                {
                    FencingToken = lowerVersionFencingToken,
                }));
        Assert.Equal(ServiceError.FencingTokenLowerVersion, setException.Reason);

        // attempt to delete the value using the lower version fencing token (it should fail)
        var deleteException = await Assert.ThrowsAsync<StateStoreOperationException>(
            async () => await stateStoreClient.DeleteAsync(
                sharedResourceName,
                new StateStoreDeleteRequestOptions()
                {
                    FencingToken = lowerVersionFencingToken,
                }));
        Assert.Equal(ServiceError.FencingTokenLowerVersion, deleteException.Reason);
    }
}
