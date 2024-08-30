namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// The base interface for all elements used in a <see cref="BlockingConcurrentDelayableQueue{T}"/>.
    /// </summary>
    public interface IDelayableQueueItem
    {
        /// <summary>
        /// Check if this queue item is ready to be dequeued.
        /// </summary>
        /// <returns>True if the queue item is ready to be dequeued.</returns>
        public bool IsReady();

        /// <summary>
        /// Mark this queue item as ready to be dequeued.
        /// </summary>
        public void MarkAsReady();
    }
}