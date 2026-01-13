namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class DeviceEndpointRuntimeHealthEventSchema
    {
        /// <summary>
        /// The runtime health of the specific inbound endpoint details as specified in the topic.
        /// </summary>
        public RuntimeHealth RuntimeHealth { get; set; } = default!;
    }
}
