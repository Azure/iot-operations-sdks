using System;

namespace Azure.Iot.Operations.Protocol.RPC
{
    /// <summary>
    /// An Exception to be thrown by Command execution code when an invalid request is processed.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InvocationException"/> class.
    /// </remarks>
    /// <param name="statusMessage">An optional human-readable status message.</param>
    /// <param name="propertyName">An optional name of the property that is invalid.</param>
    /// <param name="propertyValue">An optional string representation of the invalid property value.</param>
    public class InvocationException(string? statusMessage = null, string? propertyName = null, string? propertyValue = null) : Exception(statusMessage)
    {

        /// <summary>
        /// An optional name of the property that is invalid.
        /// </summary>
        public string? InvalidPropertyName { get; } = propertyName;

        /// <summary>
        /// An optional string representation of the invalid property value.
        /// </summary>
        public string? InvalidPropertyValue { get; } = propertyValue;
    }
}
