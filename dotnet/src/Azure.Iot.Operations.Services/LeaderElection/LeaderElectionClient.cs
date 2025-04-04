// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.LeasedLock;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Retry;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    /// <summary>
    /// A client that uses the distributed State Store to perform leader election.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once elected, this client will not automatically renew its position by default. This client allows you to opt-in to auto-renew
    /// with <see cref="AutomaticRenewalOptions"/>, though.
    /// </para>
    /// <para>
    /// When a leader is elected via <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/>,
    /// the service will respond with a fencing token via <see cref="CampaignResponse.FencingToken"/>. This fencing token
    /// allows for State Store set/delete operations on shared resources without risk of race conditions.
    /// </para>
    /// </remarks>
    public class LeaderElectionClient : ILeaderElectionClient
    {
        private readonly LeasedLockClient _leasedLockClient;
        private bool _disposed = false;
        private readonly TimeSpan _retryPolicyMaxWait = TimeSpan.FromMilliseconds(200);
        private const uint _retryPolicyBaseExponent = 1;
        private const uint _retryPolicyMaxRetries = 5;

        /// <inheritdoc/>
        public event Func<object?, LeadershipChangeEventArgs, Task>? LeadershipChangeEventReceivedAsync;

        /// <inheritdoc/>
        public string CandidateName
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _leasedLockClient.LockHolderName;
            }
        }

        /// <inheritdoc/>
        public LeaderElectionAutomaticRenewalOptions AutomaticRenewalOptions
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                return new LeaderElectionAutomaticRenewalOptions()
                {
                    AutomaticRenewal = _leasedLockClient.AutomaticRenewalOptions.AutomaticRenewal,
                    ElectionTerm = _leasedLockClient.AutomaticRenewalOptions.LeaseTermLength,
                    RenewalPeriod = _leasedLockClient.AutomaticRenewalOptions.RenewalPeriod,
                };
            }
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _leasedLockClient.AutomaticRenewalOptions = new LeasedLockAutomaticRenewalOptions()
                {
                    AutomaticRenewal = value.AutomaticRenewal,
                    LeaseTermLength = value.ElectionTerm,
                    RenewalPeriod = value.RenewalPeriod,
                };
            }
        }

        /// <inheritdoc/>
        public CampaignResponse? LastKnownCampaignResult
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_leasedLockClient.MostRecentAcquireLockResponse == null)
                {
                    return null;
                }

                return new CampaignResponse(
                    _leasedLockClient.MostRecentAcquireLockResponse.Success,
                    _leasedLockClient.MostRecentAcquireLockResponse.FencingToken);
            }
        }

        /// <summary>
        /// Construct a new leader election client.
        /// </summary>
        /// <param name="applicationContext">The application context containing shared resources.</param>
        /// <param name="mqttClient">The mqtt client to use for I/O.</param>
        /// <param name="leadershipPositionId">
        /// The identifier of the leadership position that this client can campaign for. Each client that will
        /// campaign for the same leadership role must share the same value for this parameter.
        /// </param>
        /// <param name="retryPolicy">The policy used to add extra wait time after a lease becomes available to give the previous leader priority.
        /// If not provided, a default policy will be used <see cref="LeasedLockClient(IMqttPubSubClient mqttClient, string lockName, IRetryPolicy? retryPolicy = null, string? lockHolderName = null)"/>.</param>
        /// <param name="candidateName">The name to represent this client. Other clients can look up the current
        /// leader's name.</param>
        public LeaderElectionClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string leadershipPositionId, string? candidateName = null, IRetryPolicy? retryPolicy = null)
        {
            if (string.IsNullOrEmpty(leadershipPositionId))
            {
                throw new ArgumentException("Must provide a non-null, non-empty leadership position id.");
            }

            retryPolicy ??= new ExponentialBackoffRetryPolicy(
                _retryPolicyMaxRetries,
                _retryPolicyBaseExponent,
                _retryPolicyMaxWait);

            _leasedLockClient = new LeasedLockClient(applicationContext, mqttClient, leadershipPositionId, candidateName, retryPolicy);
            _leasedLockClient.LockChangeEventReceivedAsync += LockChangeEventCallback;
        }

        private async Task LockChangeEventCallback(object? arg1, LockChangeEventArgs args)
        {
            if (LeadershipChangeEventReceivedAsync != null)
            {
                LeaderElectionCandidate? newLeader = args.NewLockHolder == null ? null : new LeaderElectionCandidate(args.NewLockHolder.Bytes);
                await LeadershipChangeEventReceivedAsync.Invoke(
                    this,
                    new LeadershipChangeEventArgs(newLeader, args.Timestamp)
                    {
                        NewState = args.NewState == LockState.Acquired ? LeadershipPositionState.LeaderElected : LeadershipPositionState.NoLeader,
                    }).ConfigureAwait(false);
            }
        }

        // For unit test purposes only
        internal LeaderElectionClient(LeasedLockClient leasedLockClient)
        {
            _leasedLockClient = leasedLockClient;
        }

        // For unit test purposes only
        internal LeaderElectionClient()
        {
            _leasedLockClient = new LeasedLockClient();
        }

        /// <inheritdoc/>
        public virtual async Task<CampaignResponse> TryCampaignAsync(TimeSpan electionTerm, CampaignRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new CampaignRequestOptions();

            var acquireLockOptions = new AcquireLockRequestOptions()
            {
                SessionId = options.SessionId,
            };

            AcquireLockResponse acquireLockResponse =
                await _leasedLockClient.TryAcquireLockAsync(
                    electionTerm,
                    acquireLockOptions,
                    cancellationToken).ConfigureAwait(false);

            return new CampaignResponse(
                acquireLockResponse.Success,
                acquireLockResponse.FencingToken);
        }

        /// <inheritdoc/>
        public virtual async Task<CampaignResponse> CampaignAsync(TimeSpan electionTerm, CampaignRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new CampaignRequestOptions();

            var acquireLockOptions = new AcquireLockRequestOptions()
            {
                SessionId = options.SessionId,
            };

            AcquireLockResponse acquireLockResponse =
                await _leasedLockClient.AcquireLockAsync(
                    electionTerm,
                    acquireLockOptions,
                    cancellationToken).ConfigureAwait(false);

            return new CampaignResponse(
                acquireLockResponse.Success,
                acquireLockResponse.FencingToken);
        }

        /// <inheritdoc/>
        public async Task CampaignAndUpdateValueAsync(StateStoreKey key, Func<StateStoreValue?, StateStoreValue?> updateValueFunc, TimeSpan? maximumTermLength = null, CancellationToken cancellationToken = default)
        {
            await _leasedLockClient.AcquireLockAndUpdateValueAsync(key, updateValueFunc, maximumTermLength, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async Task<GetCurrentLeaderResponse> GetCurrentLeaderAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            GetLockHolderResponse getLockHolderResponse =
                await _leasedLockClient.GetLockHolderAsync(cancellationToken).ConfigureAwait(false);

            if (getLockHolderResponse.LockHolder == null)
            {
                return new GetCurrentLeaderResponse(null);
            }

            return new GetCurrentLeaderResponse(new LeaderElectionCandidate(getLockHolderResponse.LockHolder.Bytes));
        }

        /// <inheritdoc/>
        public virtual async Task<ResignationResponse> ResignAsync(ResignationRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new ResignationRequestOptions();

            var releaseLockOptions = new ReleaseLockRequestOptions()
            {
                CancelAutomaticRenewal = options.CancelAutomaticRenewal,
                SessionId = options.SessionId,
            };

            ReleaseLockResponse releaseLockResponse =
                await _leasedLockClient.ReleaseLockAsync(releaseLockOptions, cancellationToken).ConfigureAwait(false);

            return new ResignationResponse(releaseLockResponse.Success);
        }

        /// <inheritdoc/>
        public virtual async Task ObserveLeadershipChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _leasedLockClient.ObserveLockAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public virtual async Task UnobserveLeadershipChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _leasedLockClient.UnobserveLockAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore(false).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore(disposing).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected async virtual ValueTask DisposeAsyncCore(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _leasedLockClient.LockChangeEventReceivedAsync -= LockChangeEventCallback;

            if (disposing)
            {
                await _leasedLockClient.DisposeAsync(disposing).ConfigureAwait(false);
            }

            _disposed = true;
        }
    }
}
