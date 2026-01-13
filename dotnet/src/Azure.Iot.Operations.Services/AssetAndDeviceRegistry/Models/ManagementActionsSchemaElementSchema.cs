namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    public partial class ManagementActionsSchemaElementSchema
    {
        /// <summary>
        /// The name of the management action for which the runtime health is being reported.
        /// </summary>
        public string ManagementActionName { get; set; } = default!;

        /// <summary>
        /// The name of the management group for which the runtime health is being reported.
        /// </summary>
        public string ManagementGroupName { get; set; } = default!;

        /// <summary>
        /// The runtime health of the specific management action.
        /// </summary>
        public RuntimeHealth RuntimeHealth { get; set; } = default!;
    }
}
