// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// The interface for interacting with a generic state store.
    /// </summary>
    /// <remarks>
    /// <see cref="StateStoreClient"/> provides an implementation of this interface for the state store that is part of Akri MQ.
    /// </remarks>
    public interface IStateStoreClient : IAsyncDisposable
    {
        /// <summary>
        /// The event that executes each time an observed key is changed.
        /// </summary>
        event Func<object?, KeyChangeMessageReceivedEventArgs, Task>? KeyChangeMessageReceivedAsync;

        /// <summary>
        /// Get the value of a key from the State Store.
        /// </summary>
        /// <param name="key">The key to get the value of.</param>
        /// <param name="requestTimeout">The optional timeout for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The service response containing the current value of the key in the State Store.</returns>
        Task<StateStoreGetResponse> GetAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Set the value of a key in the State Store.
        /// </summary>
        /// <param name="key">The key to set in the State Store.</param>
        /// <param name="value">The value to assign <paramref name="key"/> in the State Store.</param>
        /// <param name="options">The parameters for this request including the key and value to use. <see cref="StateStoreSetRequestOptions.Condition"/> allows for conditional setting/overwriting of values.</param>
        /// <param name="requestTimeout">The optional timeout for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The service response detailing if the operation succeeded and (optionally) the previous value of this key.</returns>
        Task<StateStoreSetResponse> SetAsync(StateStoreKey key, StateStoreValue value, StateStoreSetRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete the provided key from the State Store.
        /// </summary>
        /// <param name="key">The key to delete from the State Store.</param>
        /// <param name="options">The optional parameters for this request. <see cref="StateStoreDeleteRequestOptions.OnlyDeleteIfValueEquals"/> allows for conditional deletion.</param>
        /// <param name="requestTimeout">The request timeout.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The details of the service response.</returns>
        Task<StateStoreDeleteResponse> DeleteAsync(StateStoreKey key, StateStoreDeleteRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Begin receiving events each time the provided key is updated, deleted, or created. Events will be delivered
        /// via <see cref="KeyChangeMessageReceivedAsync"/>.
        /// </summary>
        /// <param name="key">The key to receive notifications about.</param>
        /// <param name="requestTimeout">The optional timeout for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method does not support using wildcard characters to subscribe to multiple keys at once.
        /// </remarks>
        Task ObserveAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop receiving events each time the provided key is updated, created, or deleted.
        /// </summary>
        /// <param name="key">The key to stop receiving events about.</param>
        /// <param name="requestTimeout">The optional timeout for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method does not support using wildcard characters to unsubscribe from multiple keys at once.
        /// </remarks>
        Task UnobserveAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);

        ValueTask DisposeAsync(bool disposing);
    }
}
