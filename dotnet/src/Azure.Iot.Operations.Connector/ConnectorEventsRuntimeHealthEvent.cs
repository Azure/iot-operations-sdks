namespace Azure.Iot.Operations.Connector
{
    using System;

    public partial class ConnectorEventsRuntimeHealthEvent
    {
        /// <summary>
        /// The name of the event group containing the event for which the runtime health is being reported.
        /// </summary>
        public string EventGroupName { get; set; } = default!;

        /// <summary>
        /// The name of the event for which the runtime health is being reported.
        /// </summary>
        public string EventName { get; set; } = default!;

        /// <summary>
        /// The runtime health of the specific event.
        /// </summary>
        public ConnectorRuntimeHealth RuntimeHealth { get; set; } = default!;
    }
}
