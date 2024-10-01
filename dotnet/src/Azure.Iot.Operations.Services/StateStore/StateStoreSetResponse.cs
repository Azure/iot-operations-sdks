using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreSetResponse : StateStoreResponse
    {
        /// <summary>
        /// True if the set request executed successfully. False otherwise.
        /// </summary>
        public bool Success { get; internal set; }

        internal StateStoreSetResponse(HybridLogicalClock version, bool success)
            : base(version)
        {
            Success = success;
        }
    }
}