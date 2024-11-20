using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    /// <summary>
    /// Interface for a cache of command responses that may be used for two purposes.
    /// 
    /// First, for ensuring that duplicated requests do not result in repeated executions, a stored response whose correlation data matches that of a previous request
    /// SHOULD return the same response at least until the expiration time of the command. This is RECOMMENDED for all commands, but it is REQUIRED for non-idempotent
    /// commands, becasue repeated non-idempotent requests will cause corruption.
    /// 
    /// Second, for reducing the load on the execution engine, a stored response whose request payload matches that of a previous request MAY be returned instead of
    /// executing the command, as long as the stored respone is returned before the specfied maximum staleness time.
    /// </summary>
    internal interface ICommandResponseCache
    {
        /// <summary>
        /// Store an <see cref="MqttApplicationMessage"/> for future retrieval, if the caching policy determines that storage is appropriate.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="invokerId">The MQTT client ID of the command invoker if it is discernable, or string.Empty if the ID is not discernable.</param>
        /// <param name="correlationData">Correlation data associated with the request; used to identify duplicate requests.</param>
        /// <param name="requestPayload">The payload of the request whose response is to be stored, used to identify matching requests.</param>
        /// <param name="responseMessage">The response message to be stored.</param>
        /// <param name="isIdempotent">True if the command is designated as idempotent.</param>
        /// <param name="commandExpirationTime">Time prior to which the command instance (identfied by <paramref name="correlationData"/>) remains valid\.</param>
        /// <param name="ttl">Time prior to which a potentially stale cached response may be returned for a matching <paramref name="requestPayload"/> but different <paramref name="correlationData"/>.</param>
        /// <param name="executionDuration">Time taken to execute the command and serialize the response into a message.</param>
        Task StoreAsync(string commandName, string invokerId, byte[] correlationData, byte[]? requestPayload, MqttApplicationMessage responseMessage, bool isIdempotent, DateTime commandExpirationTime, DateTime ttl, TimeSpan executionDuration);

        /// <summary>
        /// Retrieve a promise (Task) of an <see cref="MqttApplicationMessage"/> if present in the cache.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="invokerId">The MQTT client ID of the command invoker if it is discernable, or string.Empty if the ID is not discernable.</param>
        /// <param name="correlationData">The correlation data provided when the message was stored.</param>
        /// <param name="requestPayload">The payload of the request whose corresponding response was stored.</param>
        /// <param name="isCacheable">True if the command is cacheable for responding to a request with different <paramref name="correlationData"/>.</param>
        /// <param name="canReuseAcrossInvokers">True if the <paramref name="invokerId"/> can be ignored when caching for reuse.</param>
        /// <returns>A <c>Task</c> that promises the retrieved message, or null if the response message is neither present nor expected.</returns>
        Task<Task<MqttApplicationMessage>?> RetrieveAsync(string commandName, string invokerId, byte[] correlationData, byte[] requestPayload, bool isCacheable, bool canReuseAcrossInvokers);

        /// <summary>
        /// Start background maintenance threads.
        /// </summary>
        /// <returns>A <c>Task</c> that completes when the maintenance threads are started.</returns>
        Task StartAsync();

        /// <summary>
        /// Stop background maintenance threads.
        /// </summary>
        /// <returns>A <c>Task</c> that completes when the maintenance threads are stopped.</returns>
        Task StopAsync();
    }
}
