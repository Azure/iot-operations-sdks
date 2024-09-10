using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.LeasedLock
{
    ///<summary>
    /// A client to facilitate leased lock operations on a specific, provided lock name.
    ///</summary>
    ///<remarks>
    /// <para>
    /// Once acquired, a lock will not be automatically renewed by default. This client allows you to opt-in to auto-renew
    /// with <see cref="AutomaticRenewalOptions"/>, though.
    /// </para>
    /// <para>
    /// When a lock is granted via <see cref="AcquireLockAsync(TimeSpan, AcquireLockRequestOptions?, CancellationToken)"/>,
    /// the service will respond with a fencing token via <see cref="AcquireLockResponse.FencingToken"/>. This fencing token
    /// allows for State Store set/delete operations on shared resources without risk of race conditions.
    /// </para>
    /// </remarks>
    public class LeasedLockClient : IAsyncDisposable
    {
        private readonly IStateStoreClient _stateStoreClient;
        private readonly string _lockKey;
        private const string ValueFormat = "{0}:{1}";

        private System.Timers.Timer? _automaticRenewalTimer;
        private CancellationTokenSource? _renewalTimerCancellationToken;
        private LeasedLockAutomaticRenewalOptions _automaticRenewalOptions = new LeasedLockAutomaticRenewalOptions() { AutomaticRenewal = false };
        private bool _disposed = false;
        private TaskCompletionSource? _lockFreeToAcquireTaskCompletionSource;
        private bool _isObservingLock = false;

        /// <summary>
        /// The callback that executes whenever the current holder of the lock changes.
        /// </summary>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set this callback, then
        /// call <see cref="ObserveLockAsync(ObserveLockRequestOptions?, CancellationToken)"/>
        /// To stop watching lock holder change events, call <see cref="UnobserveLockAsync(CancellationToken)"/>
        /// and then remove any handlers from this object.
        /// </remarks>
        public event Func<object?, LockChangeEventArgs, Task>? LockChangeEventReceivedAsync;

        /// <summary>
        /// The name this client uses when trying to acquire the leased lock.
        /// </summary>
        public string LockHolderName { get; private set; }

        /// <summary>
        /// The options for automatically re-acquiring a lock before the previous lease expires. By default, 
        /// no automatic re-acquiring happens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These options must be set before calling <see cref="AcquireLockAsync(TimeSpan, AcquireLockRequestOptions?, CancellationToken)"/>.
        /// Once set, the automatic renewal will begin after the first call to <see cref="AcquireLockAsync(TimeSpan, AcquireLockRequestOptions?, CancellationToken)"/>.
        /// </para>
        /// <para>
        /// The result of automatic renewals can be accessed via <see cref="MostRecentAcquireLockResponse"/>.
        /// </para>
        /// </remarks>
        public LeasedLockAutomaticRenewalOptions AutomaticRenewalOptions
        {
            get 
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _automaticRenewalOptions; 
            }
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                ArgumentNullException.ThrowIfNull(value, nameof(value));

                _automaticRenewalOptions = value;

                if (!_automaticRenewalOptions.AutomaticRenewal)
                {
                    // stop any subsequent automatic renewals
                    CancelAutomaticRenewal();
                }
            }
        }

        /// <summary>
        /// The result of the most recent attempt at acquiring the lock.
        /// </summary>
        /// <remarks>
        /// This value captures the result of automatic re-renewing of the lock with <see cref="AutomaticRenewalOptions"/>.
        /// </remarks>
        public AcquireLockResponse? MostRecentAcquireLockResponse { get; private set; }

        /// <summary>
        /// Construct a new leased lock client.
        /// </summary>
        /// <param name="mqttClient">The client to use for I/O operations.</param>
        /// <param name="lockName">The name of the lock to acquire/release.</param>
        /// <param name="lockHolderName">The name for this client that will hold a lock. Other processes 
        /// will be able to check which client holds a lock by name. By default, this is set to the MQTT client ID.
        /// </param>
        public LeasedLockClient(IMqttPubSubClient mqttClient, string lockName, string? lockHolderName = null)
        {
            if (string.IsNullOrEmpty(lockName))
            {
                throw new ArgumentException("Must provide a non-null, non-empty lock name");
            }

            _stateStoreClient = new StateStoreClient(mqttClient);
            _lockKey = lockName;

            if (lockHolderName != null)
            {
                LockHolderName = lockHolderName;
            }
            else if (mqttClient.ClientId != null)
            { 
                LockHolderName = mqttClient.ClientId;
            }
            else
            {
                throw new ArgumentNullException("Must provide either a non-null MQTT client Id or a non-null lock holder name");
            }

            _automaticRenewalOptions = new LeasedLockAutomaticRenewalOptions();
            _stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChangeNotification;
        }

        public LeasedLockClient(IStateStoreClient stateStoreClient, string lockName, string lockHolderName)
        {
            if (string.IsNullOrEmpty(lockName))
            {
                throw new ArgumentException("Must provide a non-null, non-empty lock name");
            }

            _stateStoreClient = stateStoreClient;
            _lockKey = lockName;
            LockHolderName = lockHolderName;
            _automaticRenewalOptions = new LeasedLockAutomaticRenewalOptions();
            _stateStoreClient.KeyChangeMessageReceivedAsync += OnKeyChangeNotification;
        }

        internal LeasedLockClient()
        {
            _stateStoreClient = new StateStoreClient();
            _lockKey = string.Empty;
            LockHolderName = string.Empty;
        }
        
        /// <summary>
        /// Attempt to acquire a lock with the provided name.
        /// </summary>
        /// <param name="leaseDuration">The duration for which the lock will be held. This value only has millisecond-level precision.</param>
        /// <returns>AcquireLockResponse object with result (and fencing token if the lock was successfully acquired.)</returns>
        /// <remarks>
        /// <para>
        /// Once acquired, a lock will not be automatically renewed by default. This client allows you to opt-in to auto-renew
        /// with <see cref="AutomaticRenewalOptions"/>, though.
        /// </para>
        /// <para>
        /// When acquired, a lock has a value assigned to it which follows either the format 
        /// {lockHolderName}:{sessionId} if a sessionId is provided by <paramref name="options"/> or 
        /// just {lockHolderName} if no sessionId is provided. The lock holder name is chosen 
        /// when constructing this client and a sessionId can be chosen (or omitted, by default) 
        /// each attempt to acquire a lock.
        /// </para>
        /// </remarks>
        public virtual async Task<AcquireLockResponse> TryAcquireLockAsync(TimeSpan leaseDuration, AcquireLockRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            AcquireLockResponse acquireLockResponse = await TryAcquireLockWithoutEnablingAutoRenewalAsync(leaseDuration, options, cancellationToken);
            
            if (acquireLockResponse.Success
                && AutomaticRenewalOptions != null
                && AutomaticRenewalOptions.AutomaticRenewal)
            {
                EnableAutomaticRenewal();
            }

            return acquireLockResponse;
        }

        private async Task<AcquireLockResponse> TryAcquireLockWithoutEnablingAutoRenewalAsync(TimeSpan leaseDuration, AcquireLockRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            options ??= new AcquireLockRequestOptions();

            StateStoreValue value;
            if (string.IsNullOrEmpty(options.SessionId))
            {
                value = new StateStoreValue(LockHolderName);
            }
            else
            {
                value = new StateStoreValue(string.Format(ValueFormat, LockHolderName, options.SessionId));
            }

            Debug.Assert(_lockKey != null);
            StateStoreSetResponse setResponse =
                await _stateStoreClient.SetAsync(
                    _lockKey,
                    value,
                    new StateStoreSetRequestOptions()
                    {
                        Condition = SetCondition.OnlyIfEqualOrNotSet,
                        ExpiryTime = leaseDuration,
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            LeasedLockHolder? previousLockHolder = null;
            if (setResponse.PreviousValue != null)
            {
                previousLockHolder = new LeasedLockHolder(setResponse.PreviousValue.Bytes);
            }

            MostRecentAcquireLockResponse = new AcquireLockResponse(
                setResponse.Version,
                previousLockHolder,
                setResponse.Success);

            return MostRecentAcquireLockResponse;
        }

        private void EnableAutomaticRenewal(AcquireLockRequestOptions? options = null)
        {
            // Cancel any previous auto-renewal timer + cancellation token
            CancelAutomaticRenewal();

            _automaticRenewalTimer = new System.Timers.Timer();
            _renewalTimerCancellationToken = new CancellationTokenSource();
            _automaticRenewalTimer.Interval = AutomaticRenewalOptions.RenewalPeriod.TotalMilliseconds;
            _automaticRenewalTimer.Elapsed += async (sender, args) =>
            {
                try
                {
                    MostRecentAcquireLockResponse =
                        await TryAcquireLockWithoutEnablingAutoRenewalAsync(
                            AutomaticRenewalOptions.LeaseTermLength,
                            options,
                            _renewalTimerCancellationToken.Token);
                }
                catch (OperationCanceledException)
                {
                    // The automatic renewal was cancelled, so ignore this error.
                }
                catch (ObjectDisposedException)
                {
                    // The client was disposed while attempting automatic renewal. Ignore
                    // this exception because the automatic renewal should not continue. is now moot.
                }
                catch (AkriMqttException)
                {
                    // May be thrown if the client is disposed mid-request. Safe to ignore because
                    // the client doesn't need the response anymore.
                }
                catch (Exception)
                {
                    // This default case covers for any unexpectedly thrown exceptions. Since users can dependency inject
                    // their own MQTT client into this library, we have no way of knowing what exceptions could bubble up.
                    // Like the other catch cases, though, nothing needs to be done here. If a transient error occurred, 
                    // then the next time the timer wakes up a renewal request will be re-attempted. If a non-transient error
                    // occurred or if the client is done automatically renewing, then it is irrelevant if this attempt succeed
                    // or failed.
                }
            };

            _automaticRenewalTimer.Start();
        }

        /// <summary>
        /// Await until this client has acquired the lock or cancellation is requested.
        /// </summary>
        /// <param name="leaseDuration">The duration for which the lock will be held if the lock is acquired This value only has millisecond-level precision.</param>
        /// <returns>The service response object containing the fencing token if the lock was successfully acquired.</returns>
        /// <remarks>
        /// <para>
        /// Once acquired, a lock will not be automatically renewed by default. This client allows you to opt-in to auto-renew
        /// with <see cref="AutomaticRenewalOptions"/>, though.
        /// </para>
        /// <para>
        /// When acquired, a lock has a value assigned to it which follows the format: {lockHolderName}:{sessionId}. The lock
        /// holder name is chosen when constructing this client and a sessionId can be chosen each attempt to acquire a lock.
        /// </para>
        /// <para>
        /// This function does not rely on continuous, active polling. Instead, it relies on receiving
        /// notifications about when it is possible to acquire this lock. Since it is possible
        /// that a lock may never be acquired, it is highly recommended to provided a cancellation token.
        /// </para>
        public virtual async Task<AcquireLockResponse> AcquireLockAsync(TimeSpan leaseDuration, AcquireLockRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new AcquireLockRequestOptions();

            _lockFreeToAcquireTaskCompletionSource = new TaskCompletionSource();

            try
            {
                if (!_isObservingLock)
                {
                    Debug.Assert(_lockKey != null);
                    // The user may already be observing the lock separately from this single attempt to acquire the lock, so don't 
                    // observe it if the user is already observing it.
                    await _stateStoreClient.ObserveAsync(_lockKey, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                AcquireLockResponse response;
                do
                {
                    response = await TryAcquireLockAsync(leaseDuration, options, cancellationToken).ConfigureAwait(false);

                    // The initial set call failed to acquire the lock. Now this process will wait to be notified when
                    // the key's state has changed to deleted before attempting to acquire it again.
                    if (!response.Success)
                    {
                        await _lockFreeToAcquireTaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                        _lockFreeToAcquireTaskCompletionSource = new TaskCompletionSource();
                    }
                } while (!response.Success);

                return response;
            }
            finally
            {
                if (!_isObservingLock)
                {
                    Debug.Assert(_lockKey != null);
                    // The user may be observing the lock seperately from this single attempt to acquire the lock, so don't 
                    // unobserve it if the user is still observing it.
                    await _stateStoreClient.UnobserveAsync(_lockKey, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Block until the lock is acquired, update the value of the state store resource based on
        /// its current value, then release the lock.
        /// </summary>
        /// <param name="key">The state store key whose value will be updated.</param>
        /// <param name="updateValueFunc">
        /// The function to execute after the lock is acquired. The parameter of this function contains
        /// the current value of the state store key. The return value of this function is the new value
        /// that you wish the state store key to have.
        /// </param>
        /// <param name="maximumLeaseDuration">
        /// The maximum length of time that the client will lease the lock for once acquired. Under normal circumstances,
        /// this function will release the lock after updating the value of the shared resource, but it is possible that 
        /// this client is interrupted or encounters a fatal exception. By setting a low value for this field, you limit
        /// how long the lock can be acquired for before it is released automatically by the service.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This function will always release the lock if it was acquired. Even if cancellation is requested 
        /// when the lock is acquired, this function will release the lock.
        /// </remarks>
        public async Task AcquireLockAndUpdateValueAsync(StateStoreKey key, Func<StateStoreValue?, StateStoreValue?> updateValueFunc, TimeSpan? maximumLeaseDuration = null, CancellationToken cancellationToken = default)
        {
            TimeSpan leaseDurationVerified = maximumLeaseDuration ?? TimeSpan.FromSeconds(5);

            // The lock may need to be acquired multiple times before the key is successfully updated.
            bool valueChanged = false;
            while (!valueChanged)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AcquireLockResponse acquireLockResponse = await AcquireLockAsync(leaseDurationVerified, cancellationToken: cancellationToken);

                if (!acquireLockResponse.Success)
                {
                    continue;
                }
                
                try
                {
                    StateStoreGetResponse getResponse = await _stateStoreClient.GetAsync(key, cancellationToken: cancellationToken);

                    StateStoreValue? newValue = updateValueFunc.Invoke(getResponse.Value);

                    if (newValue == null)
                    {
                        var deleteOptions = new StateStoreDeleteRequestOptions()
                        {
                            FencingToken = acquireLockResponse.FencingToken,
                        };

                        StateStoreDeleteResponse deleteResponse = await _stateStoreClient.DeleteAsync(key, deleteOptions, cancellationToken: cancellationToken);
                        valueChanged = deleteResponse.DeletedItemsCount == 1;
                    }
                    else
                    {
                        var setOptions = new StateStoreSetRequestOptions()
                        {
                            FencingToken = acquireLockResponse.FencingToken,
                        };

                        StateStoreSetResponse setResponse = await _stateStoreClient.SetAsync(key, newValue, setOptions, cancellationToken: cancellationToken);
                        valueChanged = setResponse.Success;
                    }
                }
                finally
                {
                    // Cancellation may have been requested while the lock was acquired. Even in that
                    // case, this function still needs to release the lock prior to returning
                    // so never pass along the user-supplied cancellation token.
                    //
                    // Also note that this request may fail if this process no longer owns the lock,
                    // but that case isn't a problem because that is the desired state. Because of that,
                    // there is no need to check the return value here.
                    await ReleaseLockAsync(cancellationToken: CancellationToken.None);
                }
            }
        }

        /// <summary>
        /// Get the current holder of the lock.
        /// </summary>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The details about the current holder of the lock.</returns>
        /// <remarks>
        /// When acquired, a lock has a value assigned to it which follows the format: {lockHolderName}:{sessionId}.
        /// This function will return this value so that the owner of the lock can be identified by one or both
        /// of these fields.
        /// </remarks>
        public virtual async Task<GetLockHolderResponse> GetLockHolderAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_lockKey != null);
            StateStoreGetResponse getResponse = await _stateStoreClient.GetAsync(
                _lockKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (getResponse.Value == null)
            {
                return new GetLockHolderResponse(null);
            }

            return new GetLockHolderResponse(new LeasedLockHolder(getResponse.Value.Bytes));
        }

        /// <summary>
        /// Attempt to release a lock with the provided name.
        /// </summary>
        /// <returns>The response to the request.</returns>
        public virtual async Task<ReleaseLockResponse> ReleaseLockAsync(ReleaseLockRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new ReleaseLockRequestOptions();

            StateStoreValue value;
            if (string.IsNullOrEmpty(options.SessionId))
            {
                value = new StateStoreValue(LockHolderName);
            }
            else
            {
                value = new StateStoreValue(string.Format(ValueFormat, LockHolderName, options.SessionId));
            }

            Debug.Assert(_lockKey != null);
            StateStoreDeleteResponse deleteResponse =
                await _stateStoreClient.DeleteAsync(
                    _lockKey,
                    new StateStoreDeleteRequestOptions()
                    {
                        OnlyDeleteIfValueEquals = value,
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (options.CancelAutomaticRenewal)
            {
                CancelAutomaticRenewal();
            }

            return new ReleaseLockResponse(deleteResponse.DeletedItemsCount > 0);
        }

        /// <summary>
        /// Start receiving notifications when the lock holder changes for this leased lock.
        /// </summary>
        /// <param name="options">The optional parameters for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set one or more handlers on 
        /// <see cref="LockChangeEventReceivedAsync"/>, then call this function.
        /// To stop watching lock holder change events, call <see cref="UnobserveLockAsync(CancellationToken)"/>
        /// and then remove any handlers from <see cref="LockChangeEventReceivedAsync"/>.
        /// </remarks>
        public async virtual Task ObserveLockAsync(ObserveLockRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= new ObserveLockRequestOptions();
            Debug.Assert(_lockKey != null);
            await _stateStoreClient.ObserveAsync(
                _lockKey,
                new StateStoreObserveRequestOptions()
                {
                    GetNewValue = options.GetNewValue,
                    GetPreviousValue = options.GetPreviousValue,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            
            _isObservingLock = true;
        }

        /// <summary>
        /// Stop receiving notifications when the lock holder changes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// Users who want to watch lock holder change events must first set one or more handlers on 
        /// <see cref="LockChangeEventReceivedAsync"/>, then call <see cref="ObserveLockAsync(ObserveLockRequestOptions?, CancellationToken)"/>.
        /// To stop watching lock holder change events, call this function
        /// and then remove any handlers from <see cref="LockChangeEventReceivedAsync"/>.
        /// </remarks>
        public async virtual Task UnobserveLockAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            Debug.Assert(_lockKey != null);
            await _stateStoreClient.UnobserveAsync(_lockKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            _isObservingLock = false;
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

            _stateStoreClient.KeyChangeMessageReceivedAsync -= OnKeyChangeNotification;

            CancelAutomaticRenewal();

            if (disposing)
            {
                await _stateStoreClient.DisposeAsync(disposing).ConfigureAwait(false);
            }

            _disposed = true;
        }

        private Task OnKeyChangeNotification(object? sender, KeyChangeMessageReceivedEventArgs keyChangeEventArgs)
        {
            bool isLockAvailable = keyChangeEventArgs.NewState == KeyState.Deleted;
            var lockChangeArgs = new LockChangeEventArgs(isLockAvailable ? LockState.Released : LockState.Acquired);
            if (keyChangeEventArgs.NewValue != null)
            {
                lockChangeArgs.NewLockHolder = new LeasedLockHolder(keyChangeEventArgs.NewValue.Bytes);
            }

            LockChangeEventReceivedAsync?.Invoke(sender, lockChangeArgs);

            // This handler only cares when a particular lock is deleted because that means the lock is available
            // to be acquired.
            if (keyChangeEventArgs.ChangedKey.Equals(_lockKey)
                && keyChangeEventArgs.NewState == KeyState.Deleted
                && _lockFreeToAcquireTaskCompletionSource != null)
            {
                // Wake up the thread that was waiting for the lock to be available to acquired.
                _lockFreeToAcquireTaskCompletionSource.TrySetResult();
            }

            return Task.CompletedTask;
        }

        private void CancelAutomaticRenewal()
        {
            try
            {
                if (_automaticRenewalTimer != null && _renewalTimerCancellationToken != null)
                {
                    _renewalTimerCancellationToken.Cancel();
                    _automaticRenewalTimer.Stop();
                    _renewalTimerCancellationToken.Dispose();
                    _automaticRenewalTimer.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // This object is already disposed, so there is nothing to cancel
            }
        }
    }
}