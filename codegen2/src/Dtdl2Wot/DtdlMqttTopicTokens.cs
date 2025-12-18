namespace Dtdl2Wot
{
    /// <summary>
    /// Static class that defines string values of the replaceable components used in topic patterns.
    /// </summary>
    public static class DtdlMqttTopicTokens
    {
        /// <summary>
        /// Token representing the name of a Command.
        /// </summary>
        public const string CommandName = "{commandName}";

        /// <summary>
        /// Token representing the name of a Telemetry.
        /// </summary>
        public const string TelemetryName = "{telemetryName}";

        /// <summary>
        /// Token representing the name of a Property.
        /// </summary>
        public const string PropertyName = "{propertyName}";

        /// <summary>
        /// Token representing a Property action, 'read' or 'write'.
        /// </summary>
        public const string PropertyAction = "{action}";
    }
}
