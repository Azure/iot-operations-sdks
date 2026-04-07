namespace Azure.Iot.Operations.Connector
{
    public partial class ConnectorManagementActionsRuntimeHealthEvent
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
        public ConnectorRuntimeHealth RuntimeHealth { get; set; } = default!;
    }
}
