namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class EventRuntimeHealthEventSchema
    {
        /// <summary>
        /// The name of the asset containing the events for which the runtime health is being reported.
        /// </summary>
        public string AssetName { get; set; } = default!;

        /// <summary>
        /// Array of event runtime health information.
        /// </summary>
        public List<EventsSchemaElementSchema> Events { get; set; } = default!;
    }
}
