// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Xunit;
using Xunit.Sdk;

namespace Azure.Iot.Operations.Services.IntegrationTest;

public class StateStoreClientIntegrationTests
{
    [Fact]
    public async Task TestStateStoreObjectCRUD()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        string key = Guid.NewGuid().ToString();
        string value = Guid.NewGuid().ToString();

        Assert.True((await stateStoreClient.SetAsync(key, value)).Success);

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(key);

        Assert.Equal(value, getResponse.Value);

        Assert.Equal(1, (await stateStoreClient.DeleteAsync(key)).DeletedItemsCount);

        getResponse = await stateStoreClient.GetAsync(key);

        Assert.Null(getResponse.Value);
    }

    [Fact]
    public async Task TestStateStoreObjectExpiryTime()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var key = Guid.NewGuid().ToString();
        var value = Guid.NewGuid().ToString();

        StateStoreSetResponse setResponse =
            await stateStoreClient.SetAsync(
                key,
                value,
                new StateStoreSetRequestOptions()
                {
                    ExpiryTime = TimeSpan.FromSeconds(1),
                });

        Assert.True(setResponse.Success);

        // Wait a bit for the value in DSS to expire
        await Task.Delay(TimeSpan.FromSeconds(3));

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(key);

        // No value should have been retrieved because the value expired
        // on its own earlier
        Assert.Null(getResponse.Value);
    }

    [Fact]
    public async Task TestStateStoreConditionalSet()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var key = Guid.NewGuid().ToString();
        var value = Guid.NewGuid().ToString();

        StateStoreSetResponse setResponse =
            await stateStoreClient.SetAsync(key, value);

        Assert.True(setResponse.Success);

        setResponse = await stateStoreClient.SetAsync(
            key,
            value,
            new StateStoreSetRequestOptions()
            {
                Condition = SetCondition.OnlyIfNotSet
            });

        Assert.False(setResponse.Success,
            "Setting a value on an existing key should fail when the OnlyIfNotSet flag is set");

        setResponse = await stateStoreClient.SetAsync(
            Guid.NewGuid().ToString(),
            value,
            new StateStoreSetRequestOptions()
            {
                Condition = SetCondition.OnlyIfNotSet
            });

        Assert.True(setResponse.Success,
            "Setting a value on an non-existant key should succeed when the OnlyIfNotSet flag is set");

        setResponse = await stateStoreClient.SetAsync(
            Guid.NewGuid().ToString(),
            value,
            new StateStoreSetRequestOptions()
            {
                Condition = SetCondition.OnlyIfEqualOrNotSet
            });

        Assert.True(setResponse.Success,
            "Setting a value on an non-existant key should succeed when the OnlyIfEqualOrNotSet flag is set");

        setResponse = await stateStoreClient.SetAsync(
            key, // this key was set in DSS earlier in the test
            value,
            new StateStoreSetRequestOptions()
            {
                Condition = SetCondition.OnlyIfEqualOrNotSet
            });

        Assert.True(setResponse.Success,
            "Setting a value on an existing key should succeed when the OnlyIfEqualOrNotSet flag is set on the " +
            "request and the provided value equals what is currently in DSS");

        setResponse = await stateStoreClient.SetAsync(
            key, // this key was set in DSS earlier in the test
            new StateStoreValue(Guid.NewGuid().ToString()),
            new StateStoreSetRequestOptions()
            {
                Condition = SetCondition.OnlyIfEqualOrNotSet
            });

        Assert.False(setResponse.Success,
            "Setting a value on an existing key should fail when the OnlyIfEqualOrNotSet flag is set on the " +
            "request and the key exists in the store with a different value than what is provided");
    }

    [Fact]
    public async Task TestStateStoreConditionalDelete()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var key = Guid.NewGuid().ToString();
        var value = Guid.NewGuid().ToString();

        StateStoreSetResponse setResponse =
            await stateStoreClient.SetAsync(key, value);

        Assert.True(setResponse.Success);

        StateStoreDeleteResponse deleteResponse =
            await stateStoreClient.DeleteAsync(
                key,
                new StateStoreDeleteRequestOptions()
                {
                    OnlyDeleteIfValueEquals = new StateStoreValue("this is not the current value"),
                });

        // The "OnlyDeleteIfValueEquals" flag should prevent the delete operation from happening since
        // the provided value didn't match what was in the State Store
        Assert.Equal(-1, deleteResponse.DeletedItemsCount);

        deleteResponse =
            await stateStoreClient.DeleteAsync(
                key,
                new StateStoreDeleteRequestOptions()
                {
                    OnlyDeleteIfValueEquals = value,
                });

        // The "OnlyDeleteIfValueEquals" flag should allow the delete operation since
        // the provided value did match what was in the State Store
        Assert.Equal(1, deleteResponse.DeletedItemsCount);
    }

    [Fact]
    public async Task TestStateStoreObserveSingleKey()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        KeyChangeMessageReceivedEventArgs? mostRecentKeyChange = null;
        TaskCompletionSource onKeyChange = new TaskCompletionSource();
        Task OnKeyChange(object? arg1, KeyChangeMessageReceivedEventArgs args)
        {
            mostRecentKeyChange = args;
            onKeyChange.TrySetResult();
            return Task.CompletedTask;
        }

        stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChange;

        string key = Guid.NewGuid().ToString();

        Assert.True((await stateStoreClient.SetAsync(key, Guid.NewGuid().ToString())).Success);

        await stateStoreClient.ObserveAsync(key);

        var value = Guid.NewGuid().ToString();
        Assert.True((await stateStoreClient.SetAsync(key, value)).Success);

        try
        {
            // Wait for the observe callback to execute. Should notify this thread that the key was updated.
            await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.NotNull(mostRecentKeyChange);
        Assert.Equal(KeyState.Updated, mostRecentKeyChange.NewState);
        Assert.NotNull(mostRecentKeyChange.NewValue);
        Assert.Equal(value, mostRecentKeyChange.NewValue.GetString());
        Assert.NotNull(mostRecentKeyChange.Timestamp);
        onKeyChange = new TaskCompletionSource(); // create new TCS so that we can wait for another key change later

        Assert.Equal(1, (await stateStoreClient.DeleteAsync(key)).DeletedItemsCount);
        try
        {
            // Wait for the observe callback to execute. Should notify this thread that the key was deleted.
            await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.Equal(KeyState.Deleted, mostRecentKeyChange.NewState);
    }

    [Fact]
    public async Task TestStateStoreObserveSingleKeyThatExpires()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        KeyChangeMessageReceivedEventArgs? mostRecentKeyChange = null;
        TaskCompletionSource onKeyChange = new TaskCompletionSource();
        Task OnKeyChange(object? arg1, KeyChangeMessageReceivedEventArgs args)
        {
            mostRecentKeyChange = args;
            KeyState newState = args.NewState;
            onKeyChange.TrySetResult();
            return Task.CompletedTask;
        }

        stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChange;

        string key = Guid.NewGuid().ToString();

        await stateStoreClient.ObserveAsync(key);

        Assert.True(
            (await stateStoreClient.SetAsync(
                key,
                Guid.NewGuid().ToString(),
                new StateStoreSetRequestOptions()
                {
                    ExpiryTime = TimeSpan.FromSeconds(2)
                })).Success);

        try
        {
            // Wait for the observe callback to execute. Should notify this thread that the key was updated.
            await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw FailException.ForFailure("Timed out waiting for callback to execute");
        }

        Assert.NotNull(mostRecentKeyChange);
        Assert.Equal(KeyState.Updated, mostRecentKeyChange.NewState);
        Assert.NotNull(mostRecentKeyChange.Timestamp);

        onKeyChange = new TaskCompletionSource(); // create new TCS so that we can wait for another key change later
        try
        {
            // Wait for the observe callback to execute. Should notify this thread that the key was deleted (because it expired).
            await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.Equal(KeyState.Deleted, mostRecentKeyChange.NewState);
        Assert.NotNull(mostRecentKeyChange.Timestamp);
    }

    [Fact]
    public async Task TestStateStoreUnobserveSingleKey()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        KeyChangeMessageReceivedEventArgs? mostRecentKeyChange = null;
        TaskCompletionSource onKeyChange = new TaskCompletionSource();
        Task OnKeyChange(object? arg1, KeyChangeMessageReceivedEventArgs args)
        {
            mostRecentKeyChange = args;
            onKeyChange.TrySetResult();
            return Task.CompletedTask;
        }

        stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChange;

        string key = Guid.NewGuid().ToString();

        Assert.True((await stateStoreClient.SetAsync(key, Guid.NewGuid().ToString())).Success);

        await stateStoreClient.ObserveAsync(key);

        Assert.True((await stateStoreClient.SetAsync(key, Guid.NewGuid().ToString())).Success);

        try
        {
            await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for callback to execute");
        }

        Assert.NotNull(mostRecentKeyChange);
        Assert.Equal(KeyState.Updated, mostRecentKeyChange.NewState);
        Assert.NotNull(mostRecentKeyChange.Timestamp);

        await stateStoreClient.UnobserveAsync(key);

        onKeyChange = new TaskCompletionSource(); // create new TCS so that we can wait for another key change later

        // Update and then delete the previously observed key to ensure that neither of these events can still trigger
        // a callback after unobserving this key.
        await stateStoreClient.SetAsync(key, Guid.NewGuid().ToString());
        await stateStoreClient.DeleteAsync(key);

        try
        {
            await onKeyChange.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Fail("Expected the callback to not execute now that the client isn't observing this key");
        }
        catch (TimeoutException)
        {
            // Expected result since the callback should not execute after unobserving the key that was changed.
        }
    }

    [Fact]
    public async Task UpdateKeyWithEmptyValueShouldNotThrow()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var resp = await stateStoreClient.SetAsync("keyEmpty", string.Empty);
        Assert.True(resp.Success);
    }

    [Fact]
    public async Task CreateStateStoreEntryWithLargeSizeKey()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var keyPayload = new byte[6500]; // State store has no defined "max key size", so this is pretty arbitrary
        StateStoreKey largeSizeKey = new StateStoreKey(keyPayload);
        StateStoreValue normalSizeValue = Guid.NewGuid().ToString();
        var resp = await stateStoreClient.SetAsync(largeSizeKey, normalSizeValue);
        Assert.True(resp.Success);
    }

    [Fact]
    public async Task CreateStateStoreEntryWithLargeSizeValue()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var valuePayload = new byte[65000]; // State store has no defined "max value size", so this is pretty arbitrary
        StateStoreKey normalSizeKey = Guid.NewGuid().ToString();
        StateStoreValue largeSizeValue = new StateStoreValue(valuePayload);
        var resp = await stateStoreClient.SetAsync(normalSizeKey, largeSizeValue);
        Assert.True(resp.Success);
    }

    [Fact]
    public async Task TestStateStoreEmptyValue()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        string key = Guid.NewGuid().ToString();
        string value = "";

        Assert.True((await stateStoreClient.SetAsync(key, value)).Success);

        StateStoreGetResponse getResponse = await stateStoreClient.GetAsync(key);

        Assert.Equal(value, getResponse.Value);
    }

    [Fact]
    public async Task TestKeyLengthZero()
    // ensures the proper error reason is given for a key length of zero
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        try
        {
            await stateStoreClient.GetAsync("");
        }
        catch (StateStoreOperationException e)
        {
            Assert.Equal(ServiceError.KeyLengthZero, e.Reason);
        }
    }

    [Fact]
    public async Task TestStateStoreFencingTokenSkew()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        await using var stateStoreClient = new StateStoreClient(new ApplicationContext(), mqttClient);

        var key = Guid.NewGuid().ToString();
        var value = Guid.NewGuid().ToString();

        // create a HybridLogicalClock instance with a timestamp far in the future
        var futureTimestamp = DateTime.UtcNow.AddYears(10);
        var fencingToken = new HybridLogicalClock(futureTimestamp);

        try
        {
            await stateStoreClient.SetAsync(
                key,
                value,
                new StateStoreSetRequestOptions()
                {
                    FencingToken = fencingToken
                });
        }
        catch (StateStoreOperationException e)
        {
            Assert.Equal(ServiceError.FencingTokenSkew, e.Reason);
        }
    }
}
