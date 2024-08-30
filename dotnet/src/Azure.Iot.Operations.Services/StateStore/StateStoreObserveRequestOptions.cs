namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// The optional parameters for an Observe request.
    /// </summary>
    public class StateStoreObserveRequestOptions
    {
        /// <summary>
        /// If true, notifications about this key changing will include the new value of the key after the change.
        /// If false, notifications about this key changing will not include the new value.
        /// </summary>
        /// <remarks>
        /// The new value will be set in <see cref="KeyChangeMessageReceivedEventArgs.NewValue"/>
        /// </remarks>
        public bool GetNewValue { get; set; } = false;

        /// <summary>
        /// If true, notifications about this key changing will include the previous value of the key before the change.
        /// If false, notifications about this key changing will not include the previous value.
        /// </summary>
        /// <remarks>
        /// The new value will be set in <see cref="KeyChangeMessageReceivedEventArgs.PreviousValue"/>
        /// </remarks>
        public bool GetPreviousValue { get; set; } = false;
    }
}