namespace Azure.Iot.Operations.Protocol.RPC
{
    using System;

    /// <summary>
    /// An Exception to be thrown by Command execution code when an invalid request is processed.
    /// </summary>
    public class InvocationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvocationException"/> class.
        /// </summary>
        /// <param name="statusMessage">An optional human-readable status message.</param>
        /// <param name="propertyName">An optional name of the property that is invalid.</param>
        /// <param name="propertyValue">An optional string representation of the invalid property value.</param>
        public InvocationException(string? statusMessage = null, string? propertyName = null, string? propertyValue = null)
            : base(statusMessage)
        {
            InvalidPropertyName = propertyName;
            InvalidPropertyValue = propertyValue;
        }

        /// <summary>
        /// An optional name of the property that is invalid.
        /// </summary>
        public string? InvalidPropertyName { get; }

        /// <summary>
        /// An optional string representation of the invalid property value.
        /// </summary>
        public string? InvalidPropertyValue { get; }
    }
}
