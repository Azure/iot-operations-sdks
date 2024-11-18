namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// Static class that defines string values for MQTT User Properties.
    /// </summary>
    public static class AkriSystemProperties
    {
        /// <summary>
        /// A reserved prefix for all user properties known to Azure.Iot.Operations.Protocol; custom properties from user code may not start with this prefix.
        /// </summary>
        public const string ReservedPrefix = "__";

        /// <summary>
        /// A HybridLogicalClock timestamp associated with the request or response.
        /// </summary>
        internal const string Timestamp = ReservedPrefix + "ts";

        /// <summary>
        /// A HybridLogicalClock fencing token used to protect the object of the request from conflicting updates.
        /// </summary>
        internal const string FencingToken = ReservedPrefix + "ft";

        /// <summary>
        /// User Property indicating an HTTP status code.
        /// </summary>
        public const string Status = ReservedPrefix + "stat";

        /// <summary>
        /// User Property indicating a human-readable status message; used when Status != 200 (OK).
        /// </summary>
        public const string StatusMessage = ReservedPrefix + "stMsg";

        /// <summary>
        /// User property indicating if a non-200 <see cref="Status"/> is an application-level error.
        /// </summary>
        public const string IsApplicationError = ReservedPrefix + "apErr";

        /// <summary>
        /// User Property indicating the MQTT Client ID of a Telemetry sender.
        /// </summary>
        public const string TelemetrySenderId = ReservedPrefix + "sndId";

        /// <summary>
        /// The name of an MQTT property in a request header that is missing or has an invalid value.
        /// </summary>
        internal const string InvalidPropertyName = ReservedPrefix + "propName";

        /// <summary>
        /// The value of an MQTT property in a request header that is invalid.
        /// </summary>
        internal const string InvalidPropertyValue = ReservedPrefix + "propVal";

        /// <summary>
        /// User property that indicates the protocol version of an RPC/telemetry request.
        /// </summary>
        internal const string ProtocolVersion = ReservedPrefix + "protVer";

        /// <summary>
        /// User property indicating which major versions the command executor supports. The value
        /// of this property is a space-separated list of integers like "1 2 3".
        /// </summary>
        internal const string SupportedMajorProtocolVersions = ReservedPrefix + "supProtMajVer";

        /// <summary>
        /// User property indicating what protocol version the request had.
        /// </summary>
        /// <remarks>
        /// This property is only used when a command executor rejects a command invocation because the 
        /// requested protocol version either wasn't supported or was malformed.
        /// </remarks>
        internal const string RequestedProtocolVersion = ReservedPrefix + "requestProtVer";

        /// <summary>
        /// User property indicating what client sent this request.
        /// </summary>
        internal const string SourceId = ReservedPrefix + "srcId";

        // TODO remove this once akri service is code gen'd to expect srcId instead of invId
        public const string CommandInvokerId = ReservedPrefix + "invId";
    }
}
