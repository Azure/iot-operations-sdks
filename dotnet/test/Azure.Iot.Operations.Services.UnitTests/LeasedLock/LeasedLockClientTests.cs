using Azure.Iot.Operations.Services.LeasedLock;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore.LeasedLock
{
    public class LeasedLockClientTests
    {
        [Fact]
        public async Task TryAcquireLockAsyncWithSessionIdSuccess()
        {
            // arrange
            TimeSpan leaseDuration = TimeSpan.FromSeconds(1);
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            var fencingToken = new HybridLogicalClock();
            var expectedPreviousValue = new StateStoreValue("somePreviousValue");
            StateStoreSetResponse setResponse = new StateStoreSetResponse(fencingToken, true);

            mockStateStoreClient.Setup(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue:someSessionId", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(options => options.ExpiryTime == leaseDuration),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(setResponse));

            AcquireLockRequestOptions requestOptions = new AcquireLockRequestOptions()
            {
                SessionId = "someSessionId"
            };

            // act
            AcquireLockResponse lockResponse = await leasedLockClient.TryAcquireLockAsync(
                leaseDuration,
                requestOptions,
                cancellationToken: tokenSource.Token);

            // assert
            mockStateStoreClient.Verify(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue:someSessionId", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(
                        options => options.ExpiryTime == leaseDuration
                        && options.Condition == SetCondition.OnlyIfEqualOrNotSet),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token),
                        Times.Once());

            Assert.Equal(fencingToken, lockResponse.FencingToken);
            Assert.True(lockResponse.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireLockAsyncWithSessionIdFailure()
        {
            // arrange
            TimeSpan leaseDuration = TimeSpan.FromSeconds(1);
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            var fencingToken = new HybridLogicalClock();
            var expectedPreviousValue = new StateStoreValue("somePreviousValue");
            StateStoreSetResponse setResponse = new StateStoreSetResponse(null!, false);

            mockStateStoreClient.Setup(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue:someSessionId", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(options => options.ExpiryTime == leaseDuration),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(setResponse));

            AcquireLockRequestOptions requestOptions = new AcquireLockRequestOptions()
            {
                SessionId = "someSessionId"
            };

            // act
            AcquireLockResponse lockResponse = await leasedLockClient.TryAcquireLockAsync(
                leaseDuration,
                requestOptions,
                cancellationToken: tokenSource.Token);

            // assert
            mockStateStoreClient.Verify(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue:someSessionId", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(
                        options => options.ExpiryTime == leaseDuration
                        && options.Condition == SetCondition.OnlyIfEqualOrNotSet),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token),
                        Times.Once());

            Assert.False(lockResponse.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireLockAsyncWithoutSessionIdSuccess()
        {
            // arrange
            TimeSpan leaseDuration = TimeSpan.FromSeconds(1);
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            var fencingToken = new HybridLogicalClock();
            StateStoreSetResponse setResponse = new StateStoreSetResponse(fencingToken, true);

            mockStateStoreClient.Setup(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(
                        options => options.ExpiryTime == leaseDuration),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(setResponse));

            // act
            AcquireLockResponse lockResponse = await leasedLockClient.TryAcquireLockAsync(
                leaseDuration,
                cancellationToken: tokenSource.Token);

            // assert
            mockStateStoreClient.Verify(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(
                        options => options.ExpiryTime == leaseDuration
                        && options.Condition == SetCondition.OnlyIfEqualOrNotSet),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token),
                        Times.Once());

            Assert.Equal(fencingToken, lockResponse.FencingToken);
            Assert.True(lockResponse.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireLockAsyncWithoutSessionIdFailure()
        {
            // arrange
            TimeSpan leaseDuration = TimeSpan.FromSeconds(1);
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            var fencingToken = new HybridLogicalClock();
            var expectedPreviousValue = new StateStoreValue("somePreviousValue");
            StateStoreSetResponse setResponse = new StateStoreSetResponse(null!, false);

            mockStateStoreClient.Setup(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(
                        options => options.ExpiryTime == leaseDuration),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(setResponse));

            // act
            AcquireLockResponse lockResponse = await leasedLockClient.TryAcquireLockAsync(
                leaseDuration,
                cancellationToken: tokenSource.Token);

            // assert
            mockStateStoreClient.Verify(
                mock => mock.SetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreValue>(value => value.GetString().Equals("someValue", StringComparison.Ordinal)),
                    It.Is<StateStoreSetRequestOptions>(
                        options => options.ExpiryTime == leaseDuration
                        && options.Condition == SetCondition.OnlyIfEqualOrNotSet),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token),
                        Times.Once());

            Assert.False(lockResponse.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task GetLockHolderAsyncSuccess()
        {
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            var expectedValue = new StateStoreValue("SomeCurrentValue");
            StateStoreGetResponse getResponse = new StateStoreGetResponse(new HybridLogicalClock(), expectedValue);

            mockStateStoreClient.Setup(
                mock => mock.GetAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                        .Returns(Task.FromResult(getResponse));

            GetLockHolderResponse response = await leasedLockClient.GetLockHolderAsync(tokenSource.Token);
            Assert.NotNull(response.LockHolder);
            Assert.Equal(expectedValue.GetString(), response.LockHolder.GetString());

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task ReleaseLockAsyncWithSessionIdSuccess()
        {
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            StateStoreDeleteResponse deleteResponse = new StateStoreDeleteResponse(1);

            mockStateStoreClient.Setup(
                mock => mock.DeleteAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreDeleteRequestOptions>(options =>
                        string.Equals("someValue:someSessionId", options.OnlyDeleteIfValueEquals!.GetString(), StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                        .Returns(Task.FromResult(deleteResponse));

            ReleaseLockRequestOptions options = new ReleaseLockRequestOptions()
            {
                CancelAutomaticRenewal = false,
                SessionId = "someSessionId"
            };

            ReleaseLockResponse response = await leasedLockClient.ReleaseLockAsync(options, tokenSource.Token);

            Assert.True(response.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task ReleaseLockAsyncWithSessionIdFailure()
        {
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            StateStoreDeleteResponse deleteResponse = new StateStoreDeleteResponse(0);

            mockStateStoreClient.Setup(
                mock => mock.DeleteAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreDeleteRequestOptions>(options =>
                        string.Equals("someValue:someSessionId", options.OnlyDeleteIfValueEquals!.GetString(), StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                        .Returns(Task.FromResult(deleteResponse));

            ReleaseLockRequestOptions options = new ReleaseLockRequestOptions()
            {
                CancelAutomaticRenewal = false,
                SessionId = "someSessionId"
            };

            ReleaseLockResponse response = await leasedLockClient.ReleaseLockAsync(options, tokenSource.Token);

            Assert.False(response.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task ReleaseLockAsyncWithoutSessionIdSuccess()
        {
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            StateStoreDeleteResponse deleteResponse = new StateStoreDeleteResponse(1);

            mockStateStoreClient.Setup(
                mock => mock.DeleteAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreDeleteRequestOptions>(options =>
                        string.Equals("someValue", options.OnlyDeleteIfValueEquals!.GetString(), StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                        .Returns(Task.FromResult(deleteResponse));

            ReleaseLockRequestOptions options = new ReleaseLockRequestOptions()
            {
                CancelAutomaticRenewal = false,
            };

            ReleaseLockResponse response = await leasedLockClient.ReleaseLockAsync(options, tokenSource.Token);

            Assert.True(response.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task ReleaseLockAsyncWithoutSessionIdFailure()
        {
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            StateStoreDeleteResponse deleteResponse = new StateStoreDeleteResponse(0);

            mockStateStoreClient.Setup(
                mock => mock.DeleteAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreDeleteRequestOptions>(options =>
                        string.Equals("someValue", options.OnlyDeleteIfValueEquals!.GetString(), StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                        .Returns(Task.FromResult(deleteResponse));

            ReleaseLockRequestOptions options = new ReleaseLockRequestOptions()
            {
                CancelAutomaticRenewal = false,
            };

            ReleaseLockResponse response = await leasedLockClient.ReleaseLockAsync(options, tokenSource.Token);

            Assert.False(response.Success);

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireLockAsyncChecksCancellationToken()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task AcquireLockAsyncChecksCancellationToken()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leasedLockClient.AcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task GetLockHolderAsyncChecksCancellationToken()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leasedLockClient.GetLockHolderAsync(cancellationToken: tokenSource.Token));

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task ReleaseLockAsyncChecksCancellationToken()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leasedLockClient.ReleaseLockAsync(cancellationToken: tokenSource.Token));

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireLockAsyncChecksIfDisposed()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");
            await leasedLockClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leasedLockClient.TryAcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));
        }

        [Fact]
        public async Task AcquireLockAsyncChecksIfDisposed()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");
            await leasedLockClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leasedLockClient.AcquireLockAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));
        }

        [Fact]
        public async Task GetLockHolderAsyncChecksIfDisposed()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");
            await leasedLockClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leasedLockClient.GetLockHolderAsync(tokenSource.Token));
        }

        [Fact]
        public async Task ReleaseLockAsyncChecksIfDisposed()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");
            await leasedLockClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leasedLockClient.ReleaseLockAsync(cancellationToken: tokenSource.Token));
        }

        [Fact]
        public async Task ObserveLockAsyncSuccess()
        {
            // arrange
            bool getNewValue = true;
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            mockStateStoreClient.Setup(
                mock => mock.ObserveAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreObserveRequestOptions>(
                        options => options.GetNewValue == getNewValue),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                .Returns(Task.CompletedTask);

            ObserveLockRequestOptions requestOptions = new ObserveLockRequestOptions()
            {
                GetNewValue = getNewValue,
            };

            // act
            await leasedLockClient.ObserveLockAsync(requestOptions, tokenSource.Token);

            // assert
            mockStateStoreClient.Verify(
                mock => mock.ObserveAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.Is<StateStoreObserveRequestOptions>(
                        options => options.GetNewValue == getNewValue),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token),
                        Times.Once());

            await leasedLockClient.DisposeAsync();
        }

        [Fact]
        public async Task UnobserveLockAsyncSuccess()
        {
            // arrange
            Mock<StateStoreClient> mockStateStoreClient = GetMockStateStoreClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leasedLockClient = new LeasedLockClient(mockStateStoreClient.Object, "someLockName", "someValue");

            mockStateStoreClient.Setup(
                mock => mock.UnobserveAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token))
                .Returns(Task.CompletedTask);

            // act
            await leasedLockClient.UnobserveLockAsync(tokenSource.Token);

            // assert
            mockStateStoreClient.Verify(
                mock => mock.UnobserveAsync(
                    It.Is<StateStoreKey>(value => value.GetString().Equals("someLockName", StringComparison.Ordinal)),
                    It.IsAny<TimeSpan?>(),
                    tokenSource.Token),
                        Times.Once());

            await leasedLockClient.DisposeAsync();
        }

        private static Mock<StateStoreClient> GetMockStateStoreClient()
        {
            return new Mock<StateStoreClient>();
        }
    }
}
