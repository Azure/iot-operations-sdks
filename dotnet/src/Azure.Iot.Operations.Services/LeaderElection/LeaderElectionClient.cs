// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.LeasedLock;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;

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
    public class LeaderElectionClient : IAsyncDisposable
    {
        private readonly LeasedLockClient _leasedLockClient;
        private bool _disposed = false;

        /// <summary>
        /// The callback that executes whenever the current leader changes.
        /// </summary>
        /// <remarks>
        /// Users who want to watch leadership change events must first set this callback, then
        /// call <see cref="ObserveLeadershipChangesAsync(ObserveLeadershipChangesRequestOptions?, CancellationToken)"/>.
        /// To stop watching leadership change events, call <see cref="UnobserveLeadershipChangesAsync(CancellationToken)"/>
        /// and then remove any handlers from this object.
        /// </remarks>
        public event Func<object?, LeadershipChangeEventArgs, Task>? LeadershipChangeEventReceivedAsync;

        /// <summary>
        /// The name of this client that is used when campaigning to be leader.
        /// </summary>
        public string CandidateName
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _leasedLockClient.LockHolderName;
            }
        }

        /// <summary>
        /// The options for automatically re-campaigning to be leader at the end of a term as leader.
        /// By default, no automatic renewing happens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These options must be set before calling <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/>.
        /// Once set, the automatic renewal will begin after the first call to <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/>.
        /// </para>
        /// <para>
        /// Automatic renewal will continue for as long as the leadership position can be re-acquired. If another party acquires the leadership position, then this party's auto-renewal
        /// will end. In this case, users should use <see cref="CampaignAsync(TimeSpan, CampaignRequestOptions?, CancellationToken)"/> to campaign
        /// instead to avoid polling.
        /// </para>
        /// <para>
        /// The result of automatic renewals can be accessed via <see cref="LastKnownCampaignResult"/>.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// The result of the most recently run campaign.
        /// </summary>
        /// <remarks>
        /// This value captures the result of automatic re-campaigning with <see cref="AutomaticRenewalOptions"/>.
        /// </remarks>
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
        /// <param name="mqttClient">The mqtt client to use for I/O.</param>
        /// <param name="leadershipPositionId">
        /// The identifier of the leadership position that this client can campaign for. Each client that will
        /// campaign for the same leadership role must share the same value for this parameter.
        /// </param>
        /// <param name="candidateName">The name to represent this client. Other clients can look up the current
        /// leader's name.</param>
        public LeaderElectionClient(IMqttPubSubClient mqttClient, string leadershipPositionId, string? candidateName = null)
        {
            if (string.IsNullOrEmpty(leadershipPositionId))
            {
                throw new ArgumentException("Must provide a non-null, non-empty leadership position id.");
            }

            _leasedLockClient = new LeasedLockClient(mqttClient, leadershipPositionId, candidateName);
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

        /// <summary>
        /// Make a single attempt to campaign to be leader.
        /// </summary>
        /// <param name="electionTerm">How long the client will be leader if elected. This value only has millisecond-level precision.</param>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the campaign.</returns>
        /// <remarks>
        /// <para>
        /// Once elected, this client will not automatically renew its position by default. This client allows you to opt-in to auto-renew
        /// with <see cref="AutomaticRenewalOptions"/>, though.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Await until this client is elected leader or cancellation is requested.
        /// </summary>
        /// <param name="electionTerm">How long the client will be leader if elected. This value only has millisecond-level precision.</param>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the campaign.</returns>
        /// <remarks>
        /// <para>
        /// Once elected, this client will not automatically renew its position by default. This client allows you to opt-in to auto-renew
        /// with <see cref="AutomaticRenewalOptions"/>, though.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Block until elected leader, update the value of the state store resource based on
        /// its current value, then resign.
        /// </summary>
        /// <param name="key">The state store key whose value will be updated.</param>
        /// <param name="updateValueFunc">
        /// The function to execute after elected leader. The parameter of this function contains
        /// the current value of the state store key. The return value of this function is the new value
        /// that you wish the state store key to have.
        /// </param>
        /// <param name="maximumTermLength">
        /// The maximum length of time that the client will be leader once elected. Under normal circumstances,
        /// this function will resign from the leadership position after updating the value of the shared resource, but
        /// it is possible that this client is interrupted or encounters a fatal exception. By setting a low value for this field,
        /// you limit how long the leadership position can be acquired for before it is released automatically by the service.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This function will always resign from the leadership position if it was elected. Even if cancellation is requested
        /// after being elected leader, this function will resign from that position.
        /// </remarks>
        public async Task CampaignAndUpdateValueAsync(StateStoreKey key, Func<StateStoreValue?, StateStoreValue?> updateValueFunc, TimeSpan? maximumTermLength = null, CancellationToken cancellationToken = default)
        {
            await _leasedLockClient.AcquireLockAndUpdateValueAsync(key, updateValueFunc, maximumTermLength, cancellationToken);
        }

        /// <summary>
        /// Get the name of the current leader.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The details about the current leader.</returns>
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

        /// <summary>
        /// Resign from being the leader.
        /// </summary>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the attempted resignation.</returns>
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

        /// <summary>
        /// Start receiving notifications when the leader changes.
        /// </summary>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set one or more handlers on
        /// <see cref="LeadershipChangeEventReceivedAsync"/>, then call this function.
        /// To stop watching lock holder change events, call <see cref="UnobserveLeadershipChangesAsync(CancellationToken)"/>
        /// and then remove any handlers from <see cref="LeadershipChangeEventReceivedAsync"/>.
        /// </remarks>
        public virtual async Task ObserveLeadershipChangesAsync(ObserveLeadershipChangesRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new ObserveLeadershipChangesRequestOptions();
            await _leasedLockClient.ObserveLockAsync(
                new ObserveLockRequestOptions()
                {
                    GetNewValue = options.GetNewLeader,
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop receiving notifications when the leader changes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set one or more handlers on
        /// <see cref="LeadershipChangeEventReceivedAsync"/>, then call <see cref="ObserveLeadershipChangesAsync(ObserveLeadershipChangesRequestOptions?, CancellationToken)"/>.
        /// To stop watching lock holder change events, call this function
        /// and then remove any handlers from <see cref="LeadershipChangeEventReceivedAsync"/>.
        /// </remarks>
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
