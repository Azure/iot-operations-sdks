using Azure.Iot.Operations.Services.LeasedLock;
using Moq;
using Azure.Iot.Operations.Services.LeaderElection;
using Xunit;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore.LeaderElection
{
    public class LeaderElectionClientTests
    {
        [Fact]
        public async Task TryCampaignAsyncSuccess()
        {
            // arrange
            TimeSpan expectedDuration = TimeSpan.FromSeconds(1);
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var acquireLockResponse = new AcquireLockResponse(new HybridLogicalClock(), new LeasedLockHolder("somePreviousValue"), true);

            mockedLeasedLockClient.Setup(
                mock => mock.TryAcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(acquireLockResponse));

            // act
            CampaignResponse response = await leaderElectionClient.TryCampaignAsync(expectedDuration, cancellationToken: tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.TryAcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token),
                Times.Once());
            Assert.NotNull(response.LastKnownLeader);
            Assert.Equal("somePreviousValue", response.LastKnownLeader.GetString());
            Assert.True(response.IsLeader);

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task TryCampaignAsyncFailure()
        {
            // arrange
            TimeSpan expectedDuration = TimeSpan.FromSeconds(1);
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var acquireLockResponse = new AcquireLockResponse(new HybridLogicalClock(), null, false);

            mockedLeasedLockClient.Setup(
                mock => mock.TryAcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(acquireLockResponse));

            // act
            CampaignResponse response = await leaderElectionClient.TryCampaignAsync(expectedDuration, cancellationToken: tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.TryAcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token),
                Times.Once());

            Assert.Null(response.LastKnownLeader);
            Assert.False(response.IsLeader);

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task CampaignAsyncSuccess()
        {
            // arrange
            TimeSpan expectedDuration = TimeSpan.FromSeconds(1);
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var acquireLockResponse = new AcquireLockResponse(new HybridLogicalClock(), new LeasedLockHolder("somePreviousValue"), true);

            mockedLeasedLockClient.Setup(
                mock => mock.AcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(acquireLockResponse));

            // act
            CampaignResponse response = await leaderElectionClient.CampaignAsync(expectedDuration, cancellationToken: tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.AcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token),
                Times.Once());
            Assert.NotNull(response.LastKnownLeader);
            Assert.Equal("somePreviousValue", response.LastKnownLeader.GetString());
            Assert.True(response.IsLeader);

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task CampaignAsyncFailure()
        {
            // arrange
            TimeSpan expectedDuration = TimeSpan.FromSeconds(1);
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var acquireLockResponse = new AcquireLockResponse(new HybridLogicalClock(), null, false);

            mockedLeasedLockClient.Setup(
                mock => mock.AcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(acquireLockResponse));

            // act
            CampaignResponse response = await leaderElectionClient.CampaignAsync(expectedDuration, cancellationToken: tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.AcquireLockAsync(
                    expectedDuration,
                    It.IsAny<AcquireLockRequestOptions>(),
                    tokenSource.Token),
                Times.Once());

            Assert.Null(response.LastKnownLeader);
            Assert.False(response.IsLeader);

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task GetCurrentLeaderAsyncSuccess()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var getLockHolderResponse = new GetLockHolderResponse(new LeasedLockHolder("somePreviousValue"));

            mockedLeasedLockClient.Setup(
                mock => mock.GetLockHolderAsync(
                    tokenSource.Token))
                .Returns(Task.FromResult(getLockHolderResponse));


            // act
            GetCurrentLeaderResponse response = await leaderElectionClient.GetCurrentLeaderAsync(tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.GetLockHolderAsync(tokenSource.Token),
                Times.Once());
            Assert.NotNull(response.CurrentLeader);
            Assert.Equal("somePreviousValue", response.CurrentLeader.GetString());

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task ResignAsyncSuccess()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var releaseLockHolderResponse = new ReleaseLockResponse(true);

            mockedLeasedLockClient.Setup(
                mock => mock.ReleaseLockAsync(
                    It.IsAny<ReleaseLockRequestOptions>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(releaseLockHolderResponse));

            ResignationRequestOptions options = new ResignationRequestOptions()
            {
                CancelAutomaticRenewal = false,
            };

            // act
            ResignationResponse response = await leaderElectionClient.ResignAsync(options, tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.ReleaseLockAsync(
                    It.IsAny<ReleaseLockRequestOptions>(),
                    tokenSource.Token),
                Times.Once());

            Assert.True(response.Success);

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task ResignAsyncFailure()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            var releaseLockHolderResponse = new ReleaseLockResponse(false);

            mockedLeasedLockClient.Setup(
                mock => mock.ReleaseLockAsync(
                    It.IsAny<ReleaseLockRequestOptions>(),
                    tokenSource.Token))
                .Returns(Task.FromResult(releaseLockHolderResponse));

            ResignationRequestOptions options = new ResignationRequestOptions()
            {
                CancelAutomaticRenewal = false,
            };

            // act
            ResignationResponse response = await leaderElectionClient.ResignAsync(options, tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.ReleaseLockAsync(
                    It.IsAny<ReleaseLockRequestOptions>(),
                    tokenSource.Token),
                Times.Once());

            Assert.False(response.Success);

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task TryCampaignAsyncChecksCancellationToken()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leaderElectionClient.TryCampaignAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task CampaignAsyncChecksCancellationToken()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leaderElectionClient.CampaignAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task GetCurrentLeaderAsyncChecksCancellationToken()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leaderElectionClient.GetCurrentLeaderAsync(cancellationToken: tokenSource.Token));

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task ResignAsyncChecksCancellationToken()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            // act
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await leaderElectionClient.ResignAsync(cancellationToken: tokenSource.Token));

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task TryCampaignAsyncChecksIfDisposed()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);
            await leaderElectionClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leaderElectionClient.TryCampaignAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));
        }

        [Fact]
        public async Task CampaignAsyncChecksIfDisposed()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);
            await leaderElectionClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leaderElectionClient.CampaignAsync(TimeSpan.FromSeconds(1), cancellationToken: tokenSource.Token));
        }

        [Fact]
        public async Task GetCurrentLeaderAsyncChecksIfDisposed()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);
            await leaderElectionClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leaderElectionClient.GetCurrentLeaderAsync(tokenSource.Token));
        }

        [Fact]
        public async Task ResignAsyncChecksIfDisposed()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);
            await leaderElectionClient.DisposeAsync();

            // act, assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await leaderElectionClient.ResignAsync(cancellationToken: tokenSource.Token));
        }

        [Fact]
        public async Task ObserveLeadershipChangesAsyncSuccess()
        {
            // arrange
            bool expectedGetLastKnownLeader = true;
            bool expectedGetNewLeader = true;
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            mockedLeasedLockClient.Setup(
                mock => mock.ObserveLockAsync(
                    It.Is<ObserveLockRequestOptions>(options =>
                        options.GetNewValue == expectedGetNewLeader && options.GetPreviousValue == expectedGetLastKnownLeader),
                    tokenSource.Token));

            ObserveLeadershipChangesRequestOptions options = new ObserveLeadershipChangesRequestOptions()
            {
                GetNewLeader = expectedGetNewLeader,
                GetPreviousLeader = expectedGetLastKnownLeader
            };

            // act
            await leaderElectionClient.ObserveLeadershipChangesAsync(options, tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.ObserveLockAsync(
                    It.Is<ObserveLockRequestOptions>(options =>
                        options.GetNewValue == expectedGetNewLeader && options.GetPreviousValue == expectedGetLastKnownLeader),
                    tokenSource.Token),
                Times.Once());

            await leaderElectionClient.DisposeAsync();
        }

        [Fact]
        public async Task UnobserveLeadershipChangesAsyncSuccess()
        {
            // arrange
            Mock<LeasedLockClient> mockedLeasedLockClient = GetMockLeasedLockClient();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            var leaderElectionClient = new LeaderElectionClient(mockedLeasedLockClient.Object);

            mockedLeasedLockClient.Setup(
                mock => mock.UnobserveLockAsync(tokenSource.Token));

            // act
            await leaderElectionClient.UnobserveLeadershipChangesAsync(tokenSource.Token);

            // assert
            mockedLeasedLockClient.Verify(mock =>
                mock.UnobserveLockAsync(tokenSource.Token),
                Times.Once());

            await leaderElectionClient.DisposeAsync();
        }

        private static Mock<LeasedLockClient> GetMockLeasedLockClient()
        {
            return new Mock<LeasedLockClient>();
        }
    }
}
