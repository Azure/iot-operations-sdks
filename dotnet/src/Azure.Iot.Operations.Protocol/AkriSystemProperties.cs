// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// Static class that defines string values for MQTT User Properties.
    /// </summary>
    public static class AkriSystemProperties
    {
        /// <summary>
        /// A reserved prefix for all user properties known to Azure.Iot.Operations.Protocol. This prefix "__" should only be 
        /// used by Azure IoT Operations SDK's MQTT, Protocol, and Services packages, and any use of the 
        /// reserved prefix by consumers outside of these packages could cause unexpected behavior now or 
        /// in the future.
        /// </summary>
        public const string ReservedPrefix = "__";

        /// <summary>
        /// A HybridLogicalClock timestamp associated with the request or response.
        /// </summary>
        public const string Timestamp = ReservedPrefix + "ts";

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
        internal const string CommandInvokerId = ReservedPrefix + "invId";

        internal static bool IsReservedUserProperty(string name)
        { 
            return name.Equals(Timestamp, StringComparison.Ordinal) 
                || name.Equals(Status, StringComparison.Ordinal) 
                || name.Equals(StatusMessage, StringComparison.Ordinal) 
                || name.Equals(IsApplicationError, StringComparison.Ordinal) 
                || name.Equals(InvalidPropertyName, StringComparison.Ordinal) 
                || name.Equals(InvalidPropertyValue, StringComparison.Ordinal) 
                || name.Equals(ProtocolVersion, StringComparison.Ordinal) 
                || name.Equals(SupportedMajorProtocolVersions, StringComparison.Ordinal) 
                || name.Equals(RequestedProtocolVersion, StringComparison.Ordinal) 
                || name.Equals(SourceId, StringComparison.Ordinal) 
                || name.Equals(CommandInvokerId, StringComparison.Ordinal);
        }
    }
}
