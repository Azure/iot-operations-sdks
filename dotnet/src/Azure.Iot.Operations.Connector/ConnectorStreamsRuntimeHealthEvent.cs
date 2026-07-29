namespace Azure.Iot.Operations.Connector
{
    public partial class ConnectorStreamsRuntimeHealthEvent
    {
        /// <summary>
        /// The runtime health of the specific stream.
        /// </summary>
        public ConnectorRuntimeHealth RuntimeHealth { get; set; } = default!;

        /// <summary>
        /// The name of the stream for which the runtime health is being reported.
        /// </summary>
        public string StreamName { get; set; } = default!;
    }
}
