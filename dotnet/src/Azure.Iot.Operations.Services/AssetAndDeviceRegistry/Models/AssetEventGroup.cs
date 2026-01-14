// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    public record AssetEventGroup
    {
        /// <summary>
        /// The address of the notifier of the event group in the asset (e.g. URL) so that a client can access the event group on the asset.
        /// </summary>
        public string? DataSource { get; set; } = default;

        /// <summary>
        /// Default destinations for an event group.
        /// </summary>
        public List<EventStreamDestination>? DefaultDestinations { get; set; } = default;

        /// <summary>
        /// Stringified JSON that contains connector-specific configuration for the event group. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
        /// </summary>
        public string? EventGroupConfiguration { get; set; } = default;

        /// <summary>
        /// Array of events that are part of the event group. Each event can have per-event configuration.
        /// </summary>
        public List<AssetEvent>? Events { get; set; } = default;

        /// <summary>
        /// Name of the event group.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// URI or type definition ID.
        /// </summary>
        public string? TypeRef { get; set; } = default;
    }
}
