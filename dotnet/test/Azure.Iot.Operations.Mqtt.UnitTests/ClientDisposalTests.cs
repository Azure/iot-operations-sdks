// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.UnitTests;

namespace Azure.Iot.Operations.Mqtt.UnitTests
{
    public class ClientDisposalTests
    {
        [Fact]
        public async Task CanDisposeManagedAndUnmanagedResources()
        {
            MockMqttClient mockClient = new MockMqttClient();

            OrderedAckMqttClient orderedAckMqttClient = new(mockClient);

            await orderedAckMqttClient.DisposeAsync(true);

            Assert.True(mockClient.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeClientWithoutDisposingManagedResources()
        {
            MockMqttClient mockClient = new MockMqttClient();

            OrderedAckMqttClient orderedAckMqttClient = new(mockClient);

            await orderedAckMqttClient.DisposeAsync(false);

            Assert.False(mockClient.IsDisposed);

            MockMqttClient mockClient2 = new MockMqttClient();

            OrderedAckMqttClient orderedAckMqttClient2 = new(mockClient);

            await orderedAckMqttClient2.DisposeAsync(); // test that the default here is to not dispose managed resources

            Assert.False(mockClient2.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeManagedAndUnmanagedResourcesWithCancellation()
        {
            MockMqttClient mockClient = new MockMqttClient();

            OrderedAckMqttClient orderedAckMqttClient = new(mockClient);

            await orderedAckMqttClient.DisposeAsync(true);

            Assert.True(mockClient.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeClientWithoutDisposingManagedResourcesWithCancellation()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            MockMqttClient mockClient = new MockMqttClient();

            OrderedAckMqttClient orderedAckMqttClient = new(mockClient);

            await orderedAckMqttClient.DisposeAsync(false, cts.Token);

            Assert.False(mockClient.IsDisposed);

            MockMqttClient mockClient2 = new MockMqttClient();

            OrderedAckMqttClient orderedAckMqttClient2 = new(mockClient);

            await orderedAckMqttClient2.DisposeAsync(cts.Token); // test that the default here is to not dispose managed resources

            Assert.False(mockClient2.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeManagedAndUnmanagedResourcesSessionClient()
        {
            MockMqttClient mockClient = new MockMqttClient();

            MqttSessionClient sessionClient = new(mockClient);

            await sessionClient.DisposeAsync(true);

            Assert.True(mockClient.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeClientWithoutDisposingManagedResourcesSessionClient()
        {
            MockMqttClient mockClient = new MockMqttClient();

            MqttSessionClient sessionClient = new(mockClient);

            await sessionClient.DisposeAsync(false);

            Assert.False(mockClient.IsDisposed);

            MockMqttClient mockClient2 = new MockMqttClient();

            MqttSessionClient sessionClient2 = new(mockClient);

            await sessionClient2.DisposeAsync(); // test that the default here is to not dispose managed resources

            Assert.False(mockClient2.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeManagedAndUnmanagedResourcesWithCancellationSessionClient()
        {
            MockMqttClient mockClient = new MockMqttClient();

            MqttSessionClient sessionClient = new(mockClient);

            await sessionClient.DisposeAsync(true);

            Assert.True(mockClient.IsDisposed);
        }

        [Fact]
        public async Task CanDisposeClientWithoutDisposingManagedResourcesWithCancellationSessionClient()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            MockMqttClient mockClient = new MockMqttClient();

            MqttSessionClient sessionClient = new(mockClient);

            await sessionClient.DisposeAsync(false, cts.Token);

            Assert.False(mockClient.IsDisposed);

            MockMqttClient mockClient2 = new MockMqttClient();

            MqttSessionClient sessionClient2 = new(mockClient);

            await sessionClient2.DisposeAsync(cts.Token); // test that the default here is to not dispose managed resources

            Assert.False(mockClient2.IsDisposed);
        }
    }
}
