namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class ManagementActionRuntimeHealthEventSchema
    {
        /// <summary>
        /// The name of the asset containing the management actions for which the runtime health is being reported.
        /// </summary>
        public string AssetName { get; set; } = default!;

        /// <summary>
        /// Array of management action runtime health information.
        /// </summary>
        public List<ManagementActionsRuntimeHealth> ManagementActions { get; set; } = default!;
    }
}
