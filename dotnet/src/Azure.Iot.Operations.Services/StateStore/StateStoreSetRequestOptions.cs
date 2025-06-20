// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// The optional parameters for a Set request to the State Store
    /// </summary>
    public class StateStoreSetRequestOptions 
    {
        /**
         * persistance q's 
         * 
         * We set same user prop in MQTT packet for saving DSS key vs saving the message that carried that request?
         * 
         * Allowed values for the user property? Disk only?
         * 
         * Looks like persistance can be enabled at CONNECT, SUBSCRIBE and/or PUBLISH time?
         * 
         */
        /// <summary>
        /// The condition by which this operation will execute. By default, it will execute unconditionally.
        /// </summary>
        public SetCondition Condition { get; set; } = SetCondition.Unconditional;

        /// <summary>
        /// How long this new value will last in the State Store. If null, the value will never expire.
        /// </summary>
        /// <remarks>
        /// This value only has millisecond-level precision.
        /// </remarks>
        public TimeSpan? ExpiryTime { get; set; } = null;

        /// <summary>
        /// The optional fencing token to include in the request.
        /// </summary>
        public HybridLogicalClock? FencingToken { get; set; }
    }
}
