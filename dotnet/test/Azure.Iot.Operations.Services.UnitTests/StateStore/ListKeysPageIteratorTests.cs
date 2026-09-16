// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.StateStore;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore
{
    public class ListKeysPageIteratorTests
    {
        [Fact]
        public async Task GetNextPageAsyncReturnsFinalPageThenNull()
        {
            // arrange
            byte[] pattern = Encoding.ASCII.GetBytes("key*");
            byte[] expectedRequest =
                Encoding.ASCII.GetBytes("*2\r\n$4\r\nSCAN\r\n$4\r\nkey*\r\n");
            TimeSpan requestTimeout = TimeSpan.FromSeconds(10);
            using var cancellationTokenSource = new CancellationTokenSource();
            var clientStub = new Mock<IStateStoreClientStub>();
            clientStub
                .Setup(
                    mock => mock.InvokeAsync(
                        It.IsAny<byte[]>(),
                        null,
                        null,
                        requestTimeout,
                        cancellationTokenSource.Token))
                .Returns(CreateResponse("*1\r\n*2\r\n$4\r\nkey1\r\n$4\r\nkey2\r\n"));

            var iterator =
                new ListKeysPageIterator(clientStub.Object, pattern, requestTimeout);
            pattern[0] = (byte)'x';

            // act
            IReadOnlyList<StateStoreKey>? page =
                await iterator.GetNextPageAsync(cancellationTokenSource.Token);
            IReadOnlyList<StateStoreKey>? completedPage =
                await iterator.GetNextPageAsync(cancellationTokenSource.Token);

            // assert
            Assert.NotNull(page);
            Assert.Equal(["key1", "key2"], page.Select(key => key.GetString()));
            Assert.Null(completedPage);
            clientStub.Verify(
                mock => mock.InvokeAsync(
                    It.Is<byte[]>(request => request.SequenceEqual(expectedRequest)),
                    null,
                    null,
                    requestTimeout,
                    cancellationTokenSource.Token),
                Times.Once());
        }

        [Fact]
        public async Task GetNextPageAsyncUsesContinuationTokenAcrossPages()
        {
            // arrange
            byte[] initialRequest =
                Encoding.ASCII.GetBytes("*2\r\n$4\r\nSCAN\r\n$4\r\nkey*\r\n");
            byte[] continuationRequest =
                Encoding.ASCII.GetBytes(
                    "*3\r\n$4\r\nSCAN\r\n$4\r\nkey*\r\n$4\r\n1;1;\r\n");
            var clientStub = new Mock<IStateStoreClientStub>();
            clientStub
                .SetupSequence(
                    mock => mock.InvokeAsync(
                        It.IsAny<byte[]>(),
                        null,
                        null,
                        null,
                        CancellationToken.None))
                .Returns(CreateResponse("*2\r\n*0\r\n$4\r\n1;1;\r\n"))
                .Returns(CreateResponse("*1\r\n*1\r\n$4\r\nkey1\r\n"));

            var iterator =
                new ListKeysPageIterator(
                    clientStub.Object,
                    Encoding.ASCII.GetBytes("key*"),
                    null);

            // act
            IReadOnlyList<StateStoreKey>? firstPage =
                await iterator.GetNextPageAsync();
            IReadOnlyList<StateStoreKey>? secondPage =
                await iterator.GetNextPageAsync();
            IReadOnlyList<StateStoreKey>? completedPage =
                await iterator.GetNextPageAsync();

            // assert
            Assert.NotNull(firstPage);
            Assert.Empty(firstPage);
            Assert.NotNull(secondPage);
            Assert.Equal("key1", Assert.Single(secondPage).GetString());
            Assert.Null(completedPage);
            clientStub.Verify(
                mock => mock.InvokeAsync(
                    It.Is<byte[]>(request => request.SequenceEqual(initialRequest)),
                    null,
                    null,
                    null,
                    CancellationToken.None),
                Times.Once());
            clientStub.Verify(
                mock => mock.InvokeAsync(
                    It.Is<byte[]>(request => request.SequenceEqual(continuationRequest)),
                    null,
                    null,
                    null,
                    CancellationToken.None),
                Times.Once());
        }

        [Fact]
        public async Task GetNextPageAsyncPreservesContinuationTokenAfterFailedRequest()
        {
            // arrange
            byte[] continuationRequest =
                Encoding.ASCII.GetBytes(
                    "*3\r\n$4\r\nSCAN\r\n$4\r\nkey*\r\n$4\r\n1;1;\r\n");
            var clientStub = new Mock<IStateStoreClientStub>();
            clientStub
                .SetupSequence(
                    mock => mock.InvokeAsync(
                        It.IsAny<byte[]>(),
                        null,
                        null,
                        null,
                        CancellationToken.None))
                .Returns(CreateResponse("*2\r\n*1\r\n$4\r\nkey1\r\n$4\r\n1;1;\r\n"))
                .Returns(CreateResponse("*0\r\n"))
                .Returns(CreateResponse("*1\r\n*1\r\n$4\r\nkey2\r\n"));

            var iterator =
                new ListKeysPageIterator(
                    clientStub.Object,
                    Encoding.ASCII.GetBytes("key*"),
                    null);

            // act
            IReadOnlyList<StateStoreKey>? firstPage =
                await iterator.GetNextPageAsync();
            await Assert.ThrowsAsync<StateStoreOperationException>(
                () => iterator.GetNextPageAsync());
            IReadOnlyList<StateStoreKey>? retriedPage =
                await iterator.GetNextPageAsync();

            // assert
            Assert.NotNull(firstPage);
            Assert.Equal("key1", Assert.Single(firstPage).GetString());
            Assert.NotNull(retriedPage);
            Assert.Equal("key2", Assert.Single(retriedPage).GetString());
            clientStub.Verify(
                mock => mock.InvokeAsync(
                    It.Is<byte[]>(request => request.SequenceEqual(continuationRequest)),
                    null,
                    null,
                    null,
                    CancellationToken.None),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetNextPageAsyncAllowsRetryAfterMissingResponsePayload()
        {
            // arrange
            byte[] initialRequest =
                Encoding.ASCII.GetBytes("*2\r\n$4\r\nSCAN\r\n$4\r\nkey*\r\n");
            var clientStub = new Mock<IStateStoreClientStub>();
            clientStub
                .SetupSequence(
                    mock => mock.InvokeAsync(
                        It.IsAny<byte[]>(),
                        null,
                        null,
                        null,
                        CancellationToken.None))
                .Returns(CreateResponse([]))
                .Returns(CreateResponse("*1\r\n*1\r\n$4\r\nkey1\r\n"));

            var iterator =
                new ListKeysPageIterator(
                    clientStub.Object,
                    Encoding.ASCII.GetBytes("key*"),
                    null);

            // act
            await Assert.ThrowsAsync<StateStoreOperationException>(
                () => iterator.GetNextPageAsync());
            IReadOnlyList<StateStoreKey>? retriedPage =
                await iterator.GetNextPageAsync();

            // assert
            Assert.NotNull(retriedPage);
            Assert.Equal("key1", Assert.Single(retriedPage).GetString());
            clientStub.Verify(
                mock => mock.InvokeAsync(
                    It.Is<byte[]>(request => request.SequenceEqual(initialRequest)),
                    null,
                    null,
                    null,
                    CancellationToken.None),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetNextPageAsyncChecksCancellationToken()
        {
            // arrange
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var clientStub = new Mock<IStateStoreClientStub>();
            var iterator =
                new ListKeysPageIterator(
                    clientStub.Object,
                    Encoding.ASCII.GetBytes("key*"),
                    null);

            // act, assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => iterator.GetNextPageAsync(cancellationTokenSource.Token));
            clientStub.Verify(
                mock => mock.InvokeAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<CommandRequestMetadata?>(),
                    It.IsAny<Dictionary<string, string>?>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task GetNextPageAsyncRejectsConcurrentCalls()
        {
            // arrange
            var responseSource =
                new TaskCompletionSource<ExtendedResponse<byte[]>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var clientStub = new Mock<IStateStoreClientStub>();
            clientStub
                .Setup(
                    mock => mock.InvokeAsync(
                        It.IsAny<byte[]>(),
                        null,
                        null,
                        null,
                        CancellationToken.None))
                .Returns(
                    new RpcCallAsync<byte[]>(
                        responseSource.Task,
                        Guid.NewGuid()));

            var iterator =
                new ListKeysPageIterator(
                    clientStub.Object,
                    Encoding.ASCII.GetBytes("key*"),
                    null);

            // act
            Task<IReadOnlyList<StateStoreKey>?> firstRequest =
                iterator.GetNextPageAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => iterator.GetNextPageAsync());
            responseSource.SetResult(
                CreateExtendedResponse("*1\r\n*1\r\n$4\r\nkey1\r\n"));
            IReadOnlyList<StateStoreKey>? firstPage = await firstRequest;

            // assert
            Assert.NotNull(firstPage);
            Assert.Equal("key1", Assert.Single(firstPage).GetString());
        }

        private static RpcCallAsync<byte[]> CreateResponse(string payload)
        {
            return CreateResponse(Encoding.ASCII.GetBytes(payload));
        }

        private static RpcCallAsync<byte[]> CreateResponse(byte[] payload)
        {
            return new RpcCallAsync<byte[]>(
                Task.FromResult(CreateExtendedResponse(payload)),
                Guid.NewGuid());
        }

        private static ExtendedResponse<byte[]> CreateExtendedResponse(string payload)
        {
            return CreateExtendedResponse(Encoding.ASCII.GetBytes(payload));
        }

        private static ExtendedResponse<byte[]> CreateExtendedResponse(byte[] payload)
        {
            return new ExtendedResponse<byte[]>
            {
                Response = payload,
                ResponseMetadata = new(),
            };
        }
    }
}
