namespace Akri.Dtdl.Codegen
{
    /// <summary>
    /// Static class that defines string values of the replaceable components used in topic patterns.
    /// </summary>
    public static class MqttTopicTokens
    {
        /// <summary>
        /// Token representing the ID of a service; when generated from a DTDL model, this is the ID of the DTDL Interface.
        /// </summary>
        public const string ModelId = "{modelId}";

        /// <summary>
        /// Token representing the name of a Command.
        /// </summary>
        public const string CommandName = "{commandName}";

        /// <summary>
        /// Token representing the ID of a Command executor, should be used only in Command topic patterns.
        /// </summary>
        public const string CommandExecutorId = "{executorId}";

        /// <summary>
        /// Token representing the MQTT Client ID of a Command invoker, should be used only in Command topic patterns.
        /// </summary>
        public const string CommandInvokerId = "{invokerClientId}";

        /// <summary>
        /// Token representing the name of a Telemetry.
        /// </summary>
        public const string TelemetryName = "{telemetryName}";

        /// <summary>
        /// Token representing the ID of a Telemetry sender, should be used only in Telemetry topic patterns.
        /// </summary>
        public const string TelemetrySenderId = "{senderId}";
    }
}
