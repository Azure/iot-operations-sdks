namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    public partial class StreamsSchemaElementSchema
    {
        /// <summary>
        /// The runtime health of the specific stream.
        /// </summary>
        public RuntimeHealth RuntimeHealth { get; set; } = default!;

        /// <summary>
        /// The name of the stream for which the runtime health is being reported.
        /// </summary>
        public string StreamName { get; set; } = default!;
    }
}
