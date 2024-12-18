// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Services.StateStore;
using Moq;
using Xunit;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore
{
    public class StateStoreClientTests
    {
        [Fact]
        public async Task GetAsyncSuccess()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*2\r\n$3\r\nGET\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Encoding.ASCII.GetBytes($"${value.Bytes.Length}\r\n{value.GetString()}\r\n");
            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };
            TimeSpan expectedRequestTimeout = TimeSpan.FromSeconds(1);

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            string clientId = "someClientId";
            CancellationToken cancellationToken = new();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            mockStateStoreGeneratedClient
                .Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        null,
                        expectedRequestTimeout,
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act
            await stateStoreClient.GetAsync(key, expectedRequestTimeout, cancellationToken: cancellationToken);

            // assert
            mockStateStoreGeneratedClient
                .Verify(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        null,
                        expectedRequestTimeout,
                        cancellationToken),
                    Times.Once());

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task SetAsyncSuccess()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*3\r\n$3\r\nSET\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n${value.Bytes.Length}\r\n{value.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Encoding.ASCII.GetBytes("+OK\r\n");
            string clientId = "someClientId";
            TimeSpan expectedRequestTimeout = TimeSpan.FromSeconds(1);
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);
            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient
                .Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        expectedRequestTimeout,
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act
            await stateStoreClient.SetAsync(key, value, null, expectedRequestTimeout, cancellationToken: cancellationToken);

            // assert
            mockStateStoreGeneratedClient.Verify(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        expectedRequestTimeout,
                        cancellationToken),
                    Times.Once());

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task DeleteAsyncSuccess()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*2\r\n$3\r\nDEL\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Encoding.ASCII.GetBytes(":1\r\n");
            string clientId = "someClientId";
            TimeSpan expectedRequestTimeout = TimeSpan.FromSeconds(1);
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient.Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        expectedRequestTimeout,
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act
            await stateStoreClient.DeleteAsync(key, null, expectedRequestTimeout, cancellationToken: cancellationToken);

            // assert
            mockStateStoreGeneratedClient.Verify(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        expectedRequestTimeout,
                        cancellationToken),
                    Times.Once());

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task GetAsyncChecksCancellationToken()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            string clientId = "someClientId";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act/assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await stateStoreClient.GetAsync(key, cancellationToken: cts.Token));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task SetAsyncChecksCancellationToken()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            string clientId = "someClientId";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act/assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await stateStoreClient.SetAsync(key, value, null, cancellationToken: cts.Token));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task DeleteAsyncChecksCancellationToken()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            string clientId = "someClientId";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act/assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await stateStoreClient.DeleteAsync(key, null, cancellationToken: cts.Token));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task GetAsyncThrowsObjectDisposedExceptionIfDisposed()
        {
            // arrange
            CancellationToken cancellationToken = new CancellationToken();
            string clientId = "someClientId";

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);
            await stateStoreClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await stateStoreClient.GetAsync(
                    new StateStoreKey(Array.Empty<byte>()),
                    cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task SetAsyncThrowsObjectDisposedExceptionIfDisposed()
        {
            // arrange
            CancellationToken cancellationToken = new CancellationToken();
            string clientId = "someClientId";

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);
            await stateStoreClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await stateStoreClient.SetAsync(
                    new StateStoreKey(Array.Empty<byte>()),
                    new StateStoreValue(Array.Empty<byte>()),
                    null,
                    cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task DeleteAsyncThrowsObjectDisposedExceptionIfDisposed()
        {
            // arrange
            CancellationToken cancellationToken = new CancellationToken();
            string clientId = "someClientId";

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);
            await stateStoreClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await stateStoreClient.DeleteAsync(
                    new StateStoreKey(Array.Empty<byte>()),
                    null,
                    cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task GetAsyncThrowsStateStoreExceptionIfNoResponsePayload()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*2\r\n$3\r\nGET\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Array.Empty<byte>();
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient.Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act, assert
            await Assert.ThrowsAsync<StateStoreOperationException>(
                async () => await stateStoreClient.GetAsync(
                    key,
                    cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task SetAsyncThrowsStateStoreExceptionIfNoResponsePayload()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*3\r\n$3\r\nSET\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n${value.Bytes.Length}\r\n{value.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Array.Empty<byte>();
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient.Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act, assert
            await Assert.ThrowsAsync<StateStoreOperationException>(
                async () => await stateStoreClient.SetAsync(
                    key,
                    value,
                    null,
                    cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task DeleteAsyncThrowsStateStoreExceptionIfNoResponsePayload()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*2\r\n$3\r\nDEL\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Array.Empty<byte>();
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient.Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act, assert
            await Assert.ThrowsAsync<StateStoreOperationException>(
                async () => await stateStoreClient.DeleteAsync(
                    key,
                    null,
                    cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task GetAsyncThrowsIfNullKey()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.GetAsync(null!, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task GetAsyncThrowsIfNullKeyBytes()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.GetAsync(new StateStoreKey((byte[])null!), cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task SetAsyncThrowsIfNullKey()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.SetAsync(null!, new StateStoreValue("someValue"), null, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task SetAsyncThrowsIfNullKeyBytes()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.SetAsync(new StateStoreKey((byte[])null!), new StateStoreValue("someValue"), null, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }


        [Fact]
        public async Task SetAsyncThrowsIfNullValue()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.SetAsync(new StateStoreKey("someKey"), null!, null, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task SetAsyncThrowsIfNullValueBytes()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.SetAsync(new StateStoreKey("someKey"), new StateStoreValue((byte[])null!), null, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task DeleteAsyncThrowsIfNullKey()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.DeleteAsync(null!, null, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task DeleteAsyncThrowsIfNullKeyBytes()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.DeleteAsync(new StateStoreKey((byte[])null!), null, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task ObserveAsyncSuccess()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*2\r\n$9\r\nKEYNOTIFY\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Encoding.ASCII.GetBytes("+OK\r\n");
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);
            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            mockMqttClient
                .Setup(
                    mock => mock.SubscribeAsync(
                        It.IsAny<MqttClientSubscribeOptions>(),
                        cancellationToken))
                .Returns(
                    Task.FromResult(new MqttClientSubscribeResult(
                        0,
                        new List<MqttClientSubscribeResultItem>()
                        {
                            new MqttClientSubscribeResultItem(
                                new MqttTopicFilter($"clients/{clientId}/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/notify"),
                                MqttClientSubscribeReasonCode.GrantedQoS1),
                        },
                        "ok",
                        new List<MqttUserProperty>())));

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient
                .Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act
            await stateStoreClient.ObserveAsync(key, null, cancellationToken: cancellationToken);

            // assert
            mockStateStoreGeneratedClient.Verify(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken),
                    Times.Once());

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task UnobserveAsyncSuccess()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*3\r\n$9\r\nKEYNOTIFY\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n$4\r\nSTOP\r\n");

            byte[] expectedServiceResponsePayload = Encoding.ASCII.GetBytes("+OK\r\n");
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);
            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient
                .Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act
            await stateStoreClient.UnobserveAsync(key, cancellationToken: cancellationToken);

            // assert
            mockStateStoreGeneratedClient.Verify(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken),
                    Times.Once());

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task ObserveAsyncChecksCancellationToken()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            string clientId = "someClientId";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act/assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await stateStoreClient.ObserveAsync(key, default, cancellationToken: cts.Token));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task ObserveAsyncThrowsObjectDisposedExceptionIfDisposed()
        {
            // arrange
            CancellationToken cancellationToken = new CancellationToken();
            string clientId = "someClientId";

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);
            await stateStoreClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await stateStoreClient.ObserveAsync(
                    new StateStoreKey(Array.Empty<byte>()),
                    null,
                    cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task ObserveAsyncThrowsIfNullKey()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.ObserveAsync((StateStoreKey)null!, new StateStoreObserveRequestOptions(), cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task ObserveAsyncThrowsIfNullKeyBytes()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.ObserveAsync(new StateStoreKey((byte[])null!), new StateStoreObserveRequestOptions(), cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
#pragma warning disable CA1506 // Avoid excessive class coupling
        public async Task ObserveAsyncThrowsStateStoreExceptionIfNoResponsePayload()
#pragma warning restore CA1506 // Avoid excessive class coupling
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*2\r\n$9\r\nKEYNOTIFY\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n");

            byte[] expectedServiceResponsePayload = Array.Empty<byte>();
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            mockMqttClient
                .Setup(
                    mock => mock.SubscribeAsync(
                        It.IsAny<MqttClientSubscribeOptions>(),
                        cancellationToken))
                .Returns(
                    Task.FromResult(new MqttClientSubscribeResult(
                        0,
                        new List<MqttClientSubscribeResultItem>()
                        {
                            new MqttClientSubscribeResultItem(
                                new MqttTopicFilter($"clients/{clientId}/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/notify"),
                                MqttClientSubscribeReasonCode.GrantedQoS1),
                        },
                        "ok",
                        new List<MqttUserProperty>())));

            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient.Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act, assert
            await Assert.ThrowsAsync<StateStoreOperationException>(
                async () => await stateStoreClient.ObserveAsync(
                    key,
                    default,
                    cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task UnobserveAsyncChecksCancellationToken()
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            string clientId = "someClientId";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act/assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await stateStoreClient.UnobserveAsync(key, cancellationToken: cts.Token));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task UnobserveAsyncThrowsObjectDisposedExceptionIfDisposed()
        {
            // arrange
            CancellationToken cancellationToken = new CancellationToken();
            string clientId = "someClientId";

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);
            await stateStoreClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await stateStoreClient.UnobserveAsync(
                    new StateStoreKey(Array.Empty<byte>()),
                    cancellationToken: cancellationToken));
        }

        [Fact]
        public async Task UnobserveAsyncThrowsIfNullKey()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.UnobserveAsync((StateStoreKey)null!, cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task UnobserveAsyncThrowsIfNullKeyBytes()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockMqttClient = GetMockMqttClient("someClientId");

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await stateStoreClient.UnobserveAsync(new StateStoreKey((byte[])null!), cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
#pragma warning disable CA1506 // Avoid excessive class coupling
        public async Task UnobserveAsyncThrowsStateStoreExceptionIfNoResponsePayload()
#pragma warning restore CA1506 // Avoid excessive class coupling
        {
            // arrange
            StateStoreKey key = new StateStoreKey("someKey");
            StateStoreValue value = new StateStoreValue("someValue");
            byte[] expectedServiceRequestPayload = Encoding.ASCII.GetBytes($"*3\r\n$9\r\nKEYNOTIFY\r\n${key.Bytes.Length}\r\n{key.GetString()}\r\n$4\r\nSTOP\r\n");

            byte[] expectedServiceResponsePayload = Array.Empty<byte>();
            string clientId = "someClientId";
            CancellationToken cancellationToken = new CancellationToken();

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            mockMqttClient
                .Setup(
                    mock => mock.SubscribeAsync(
                        It.IsAny<MqttClientSubscribeOptions>(),
                        cancellationToken))
                .Returns(
                    Task.FromResult(new MqttClientSubscribeResult(
                        0,
                        new List<MqttClientSubscribeResultItem>()
                        {
                            new MqttClientSubscribeResultItem(
                                new MqttTopicFilter($"clients/{clientId}/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/notify"),
                                MqttClientSubscribeReasonCode.GrantedQoS1),
                        },
                        "ok",
                        new List<MqttUserProperty>())));

            ExtendedResponse<byte[]> expectedServiceResponse = new()
            {
                Response = expectedServiceResponsePayload,
                ResponseMetadata = new()
            };

            RpcCallAsync<byte[]> expectedResponse = new(Task.FromResult(expectedServiceResponse), Guid.NewGuid());

            mockStateStoreGeneratedClient.Setup(
                    mock => mock.InvokeAsync(
                        It.Is<byte[]>(array => array != null && Enumerable.SequenceEqual(array, expectedServiceRequestPayload)),
                        It.IsAny<CommandRequestMetadata>(),
                        It.IsAny<TimeSpan?>(),
                        cancellationToken))
                .Returns(expectedResponse);

            StateStoreClient stateStoreClient = new StateStoreClient(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act, assert
            await Assert.ThrowsAsync<StateStoreOperationException>(
                async () => await stateStoreClient.UnobserveAsync(
                    key,
                    cancellationToken: cancellationToken));

            await stateStoreClient.DisposeAsync();
        }

        [Fact]
        public async Task DisposeSuccess()
        {
            // arrange
            string clientId = "someClientId";

            var mockStateStoreGeneratedClient = new Mock<StateStoreGeneratedClientHolder>();
            var mockMqttClient = GetMockMqttClient(clientId);

            mockStateStoreGeneratedClient
                .Setup(
                    mock => mock.DisposeAsync());

            StateStoreClient stateStoreClient = new(mockMqttClient.Object, mockStateStoreGeneratedClient.Object);

            // act
            await stateStoreClient.DisposeAsync();

            // assert
            mockStateStoreGeneratedClient
                .Verify(
                    mock => mock.DisposeAsync(),
                    Times.Once());
        }

        private static Mock<IMqttPubSubClient> GetMockMqttClient(string clientId)
        {
            var mockMqttClient = new Mock<IMqttPubSubClient>();

            mockMqttClient.Setup(mock => mock.ClientId)
                .Returns(clientId);

            return mockMqttClient;
        }
    }
}