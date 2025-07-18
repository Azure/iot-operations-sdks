namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class AssetEventSchemaElement : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// Array of data points that are part of the event. Each data point can have per-data-point configuration.
        /// </summary>
        [JsonPropertyName("dataPoints")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<AssetEventDataPointSchemaElement>? DataPoints { get; set; } = default;

        /// <summary>
        /// Destinations for an Event.
        /// </summary>
        [JsonPropertyName("destinations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<EventStreamDestination>? Destinations { get; set; } = default;

        /// <summary>
        /// Stringified JSON that contains connector-specific configuration for the specific event.
        /// </summary>
        [JsonPropertyName("eventConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? EventConfiguration { get; set; } = default;

        /// <summary>
        /// The address of the notifier of the event in the asset (e.g. URL) so that a client can access the notifier on the asset.
        /// </summary>
        [JsonPropertyName("eventNotifier")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string EventNotifier { get; set; } = default!;

        /// <summary>
        /// The name of the event.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string Name { get; set; } = default!;

        /// <summary>
        /// URI or type definition id in companion spec.
        /// </summary>
        [JsonPropertyName("typeRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? TypeRef { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (EventNotifier is null)
            {
                throw new ArgumentNullException("eventNotifier field cannot be null");
            }
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (EventNotifier is null)
            {
                throw new ArgumentNullException("eventNotifier field cannot be null");
            }
            if (Name is null)
            {
                throw new ArgumentNullException("name field cannot be null");
            }
        }
    }
}
