// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Azure.Iot.Operations.Services.Assets
{
    public record Asset
    {
        /// <summary>
        /// Globally unique, immutable, non-reusable id.
        /// </summary>
        public string? Uuid { get; set; }

        /// <summary>
        /// Enabled/Disabled status of the asset.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Asset id provided by the customer.
        /// </summary>
        public string? ExternalAssetId { get; set; }

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Human-readable description of the asset.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// A reference to the asset endpoint profile (connection information) used by brokers to connect to an endpoint that provides data points for this asset. Must have the format <ModuleCR.metadata.namespace>/<ModuleCR.metadata.name>.
        /// </summary>
        public string? AssetEndpointProfileRef { get; set; }

        /// <summary>
        /// A value that is incremented each time the resource is modified.
        /// </summary>
        public long? Version { get; set; }

        /// <summary>
        /// Asset manufacturer name.
        /// </summary>
        public string? Manufacturer { get; set; }

        /// <summary>
        /// Asset manufacturer URI.
        /// </summary>
        public string? ManufacturerUri { get; set; }

        /// <summary>
        /// Asset model name.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Asset product code.
        /// </summary>
        public string? ProductCode { get; set; }

        /// <summary>
        /// Revision number of the hardware.
        /// </summary>
        public string? HardwareRevision { get; set; }

        /// <summary>
        /// Revision number of the software.
        /// </summary>
        public string? SoftwareRevision { get; set; }

        /// <summary>
        /// Reference to the documentation.
        /// </summary>
        public string? DocumentationUri { get; set; }

        /// <summary>
        /// Asset serial number.
        /// </summary>
        public string? SerialNumber { get; set; }

        /// <summary>
        /// A set of key-value pairs that contain custom attributes set by the customer.
        /// </summary>
        [JsonConverter(typeof(JsonDocumentConverter))]
        public JsonDocument? Attributes { get; set; }

        /// <summary>
        /// Reference to a list of discovered assets. Populated only if the asset has been created from discovery flow.
        /// </summary>
        public string[]? DiscoveredAssetRefs { get; set; }

        /// <summary>
        /// Protocol-specific default configuration for all datasets. Each dataset can have its own configuration that overrides the default settings here.
        /// </summary>
        [JsonConverter(typeof(JsonDocumentConverter))]
        public JsonDocument? DefaultDatasetsConfiguration { get; set; }

        /// <summary>
        /// Protocol-specific default configuration for all data sets. Each data set can have its own configuration that overrides the default settings here. This assumes that each asset instance has one protocol.
        /// </summary>
        [JsonConverter(typeof(JsonDocumentConverter))]
        public JsonDocument? DefaultEventsConfiguration { get; set; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? DefaultTopic { get; set; }

        /// <summary>
        /// The mapping of dataset names to datasets that are part of the asset. Each dataset can have per-dataset configuration.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, Dataset>? DatasetsDictionary
        {
            get
            {
                Dictionary<string, Dataset>? dictionary = null;
                if (Datasets != null)
                {
                    dictionary = new();
                    foreach (Dataset dataset in Datasets)
                    {
                        if (!string.IsNullOrWhiteSpace(dataset.Name))
                        {
                            dictionary[dataset.Name] = dataset;
                        }
                        else
                        {
                            Trace.TraceWarning($"Unexpected dataset with null or empty name found.");
                        }
                    }
                }

                return dictionary;
            }
        }

        public Dataset[]? Datasets { get; set; }

        /// <summary>
        /// The mapping of event names to events in this asset.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, Event>? EventsDictionary
        {
            get
            {
                Dictionary<string, Event>? dictionary = null;
                if (Events != null)
                {
                    dictionary = new();
                    foreach (Event eventInternal in Events)
                    {
                        if (!string.IsNullOrWhiteSpace(eventInternal.Name))
                        {
                            dictionary[eventInternal.Name] = eventInternal;
                        }
                        else
                        {
                            Trace.TraceWarning($"Unexpected event with null or empty name found.");
                        }
                    }
                }

                return dictionary;
            }
        }

        public Event[]? Events { get; set; }

        /// <summary>
        /// Read only object to reflect changes that have occurred on the Edge. Similar to Kubernetes status property for custom resources.
        /// </summary>
        public Status? Status { get; set; }

        /// <summary>
        /// Provisioning state of the resource.
        /// </summary>
        public string? ProvisioningState { get; set; }
    }

    public record Dataset
    {
        /// <summary>
        /// The name of the dataset.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Protocol-specific JSON string that describes configuration for the specific dataset.
        /// </summary>
        [JsonConverter(typeof(JsonDocumentConverter))]
        public JsonDocument? DatasetConfiguration { get; set; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? Topic { get; set; }

        /// <summary>
        /// The mapping of datapoint names to datapoints in this dataset.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, DataPoint>? DataPointsDictionary
        {
            get
            {
                Dictionary<string, DataPoint>? dictionary = null;
                if (DataPoints != null)
                {
                    dictionary = new();
                    foreach (DataPoint dataPointInternal in DataPoints)
                    {
                        if (!string.IsNullOrWhiteSpace(dataPointInternal.Name))
                        {
                            dictionary[dataPointInternal.Name] = dataPointInternal;
                        }
                        else
                        {
                            Trace.TraceWarning($"Unexpected datapoint with null or empty name found.");
                        }
                    }
                }

                return dictionary;
            }
        }

        public DataPoint[]? DataPoints { get; set; }
    }

    public record DataPoint
    {
        /// <summary>
        /// The name of the data point.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The address of the source of the data in the asset (e.g. URL) so that a client can access the data source on the asset.
        /// </summary>
        public string DataSource { get; set; } = string.Empty;

        /// <summary>
        /// An indication of how the data point should be mapped to OpenTelemetry.
        /// </summary>
        public string? ObservabilityMode { get; set; }

        /// <summary>
        /// Protocol-specific configuration for the data point. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
        /// </summary>
        [JsonConverter(typeof(JsonDocumentConverter))]
        public JsonDocument? DataPointConfiguration { get; set; }
    }

    public record Event
    {
        /// <summary>
        /// The name of the event.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The address of the notifier of the event in the asset (e.g. URL) so that a client can access the event on the asset.
        /// </summary>
        public string? EventNotifier { get; set; }

        /// <summary>
        /// An indication of how the event should be mapped to OpenTelemetry.
        /// </summary>
        public string? ObservabilityMode { get; set; }

        /// <summary>
        /// Protocol-specific configuration for the event. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
        /// </summary>
        [JsonConverter(typeof(JsonDocumentConverter))]
        public JsonDocument? EventConfiguration { get; set; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? Topic { get; set; }
    }

    public record Topic
    {
        /// <summary>
        /// The topic path for messages sent for the specific entry.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The topic retain attribute for the specific entry.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter<RetainHandling>))]
        public RetainHandling? Retain { get; set; }
    }

    public enum RetainHandling
    {
        /// <summary>
        /// If it was retain on source, then re-publish on MQ as retain.
        /// </summary>
        Keep,

        /// <summary>
        /// Never publish as retain.
        /// </summary>
        Never,
    }

    public record Status
    {
        /// <summary>
        /// Array object to transfer and persist errors that originate from the Edge.
        /// </summary>
        public StatusError[]? Errors { get; set; }

        /// <summary>
        /// A read only incremental counter indicating the number of times the configuration has been modified from the perspective of the current actual (Edge) state of the Asset. Edge would be the only writer of this value and would sync back up to the cloud. In steady state, this should equal version.
        /// </summary>
        public long? Version { get; set; }

        /// <summary>
        /// The mapping of status dataset names to status datasets in this status.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, StatusDatasets>? DatasetsDictionary
        {
            get
            {
                Dictionary<string, StatusDatasets>? dictionary = null;
                if (Datasets != null)
                {
                    dictionary = new();
                    foreach (StatusDatasets statusDataset in Datasets)
                    {
                        if (!string.IsNullOrWhiteSpace(statusDataset.Name))
                        {
                            dictionary[statusDataset.Name] = statusDataset;
                        }
                        else
                        {
                            Trace.TraceWarning($"Unexpected datapoint with null or empty name found.");
                        }
                    }
                }

                return dictionary;
            }
        }

        public StatusDatasets[]? Datasets { get; set; }

        /// <summary>
        /// The mapping of status event names to status events in this status.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, StatusEvents>? EventsDictionary
        {
            get
            {
                Dictionary<string, StatusEvents>? dictionary = null;
                if (Events != null)
                {
                    dictionary = new();
                    foreach (StatusEvents statusEvents in Events)
                    {
                        if (!string.IsNullOrWhiteSpace(statusEvents.Name))
                        {
                            dictionary[statusEvents.Name] = statusEvents;
                        }
                        else
                        {
                            Trace.TraceWarning($"Unexpected statusEvents with null or empty name found.");
                        }
                    }
                }

                return dictionary;
            }
        }

        public StatusEvents[]? Events { get; set; }
    }

    public record StatusError
    {
        /// <summary>
        /// Error code for classification of errors (ex: 400, 404, 500, etc.).
        /// </summary>
        public int? Code { get; set; }

        /// <summary>
        /// Human readable helpful error message to provide additional context for error (ex: “capability Id 'foo' does not exist”).
        /// </summary>
        public string? Message { get; set; }
    }

    public record StatusDatasets
    {
        /// <summary>
        /// The name of the data set. Must be unique within the status.datasets array. This name is used to correlate between the spec and status data set information.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Defines the message schema reference properties.
        /// </summary>
        public MessageSchemaReference? MessageSchemaReference { get; set; } //TODO this should be populated during sample w/ the MQTT message schema
    }

    public record StatusEvents
    {
        /// <summary>
        /// The name of the event. Must be unique within the status.events array. This name is used to correlate between the spec and status event information.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Defines the message schema reference properties.
        /// </summary>
        public MessageSchemaReference? MessageSchemaReference { get; set; }
    }

    public record MessageSchemaReference
    {
        /// <summary>
        /// The reference to the message schema registry namespace.
        /// </summary>
        public string? SchemaRegistryNamespace { get; set; }

        /// <summary>
        /// The reference to the message schema name.
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// The reference to the message schema version.
        /// </summary>
        public string? SchemaVersion { get; set; }
    }
}
