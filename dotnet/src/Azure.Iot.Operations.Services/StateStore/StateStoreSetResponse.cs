using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreSetResponse : StateStoreResponse
    {
        /// <summary>
        /// The previous value assigned to the State Store key. This value is null if it
        /// was not requested via <see cref="StateStoreSetRequestOptions.GetPreviousValue"/>
        /// or if it had no previous value because this operation created it.
        /// </summary>
        public StateStoreValue? PreviousValue { get; internal set; }

        /// <summary>
        /// True if the set request executed successfully. False otherwise.
        /// </summary>
        public bool Success { get; internal set; }

        internal StateStoreSetResponse(HybridLogicalClock version, bool success)
            : base(version)
        {
            Success = success;
            PreviousValue = null;
        }
    }
}