namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class StreamRuntimeHealthEventSchema
    {
        /// <summary>
        /// The name of the asset containing the streams for which the runtime health is being reported.
        /// </summary>
        public string AssetName { get; set; } = default!;

        /// <summary>
        /// Array of stream runtime health information.
        /// </summary>
        public List<StreamsRuntimeHealth> Streams { get; set; } = default!;
    }
}
