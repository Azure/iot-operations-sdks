// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.CloudEvents;

/// <summary>
/// Determines the Cloud Event headers of data forwarded with
/// <see cref="AssetClient.ForwardSampledDatasetWithCloudEventAsync"/> or
/// <see cref="AssetClient.ForwardReceivedEventWithCloudEventAsync"/>.
/// </summary>
/// <remarks>
/// Values that aren't set are <c>Automatic</c>, and automatic values are determined when the data is forwarded.
/// Other Azure IoT Operations components may rely on the automatic values, so only change them if the data's
/// consumers expect it. Cloud Event headers are only sent for MQTT destinations.
/// </remarks>
public sealed class DataOperationCloudEvent
{
    private readonly CloudEventSource _source = CloudEventSource.Automatic;
    private readonly CloudEventType _type = CloudEventType.Automatic;
    private readonly CloudEventSubject _subject = CloudEventSubject.Automatic;
    private readonly CloudEventDataSchema _dataSchema = CloudEventDataSchema.Automatic;
    private readonly CloudEventId _id = CloudEventId.Automatic;
    private readonly CloudEventTime _time = CloudEventTime.Automatic;
    private readonly CloudEventAioDeviceRef _aioDeviceRef = CloudEventAioDeviceRef.Automatic;
    private readonly CloudEventAioAssetRef _aioAssetRef = CloudEventAioAssetRef.Automatic;

    /// <summary>
    /// Identifies the context in which the event happened. Defaults to <see cref="CloudEventSource.Automatic"/>.
    /// </summary>
    public CloudEventSource Source { get => _source; init => _source = value ?? CloudEventSource.Automatic; }

    /// <summary>
    /// Describes the type of event. Defaults to <see cref="CloudEventType.Automatic"/>.
    /// </summary>
    public CloudEventType Type { get => _type; init => _type = value ?? CloudEventType.Automatic; }

    /// <summary>
    /// Identifies the subject of the event within the <c>source</c>. Defaults to <see cref="CloudEventSubject.Automatic"/>.
    /// </summary>
    public CloudEventSubject Subject { get => _subject; init => _subject = value ?? CloudEventSubject.Automatic; }

    /// <summary>
    /// Identifies the schema that the data adheres to. Defaults to <see cref="CloudEventDataSchema.Automatic"/>.
    /// </summary>
    public CloudEventDataSchema DataSchema { get => _dataSchema; init => _dataSchema = value ?? CloudEventDataSchema.Automatic; }

    /// <summary>
    /// Identifies the event. Defaults to <see cref="CloudEventId.Automatic"/>.
    /// </summary>
    public CloudEventId Id { get => _id; init => _id = value ?? CloudEventId.Automatic; }

    /// <summary>
    /// Timestamp of when the occurrence happened. Defaults to <see cref="CloudEventTime.Automatic"/>.
    /// </summary>
    public CloudEventTime Time { get => _time; init => _time = value ?? CloudEventTime.Automatic; }

    /// <summary>
    /// AIO extension referencing the device and inbound endpoint. Defaults to <see cref="CloudEventAioDeviceRef.Automatic"/>.
    /// </summary>
    public CloudEventAioDeviceRef AioDeviceRef { get => _aioDeviceRef; init => _aioDeviceRef = value ?? CloudEventAioDeviceRef.Automatic; }

    /// <summary>
    /// AIO extension referencing the asset. Defaults to <see cref="CloudEventAioAssetRef.Automatic"/>.
    /// </summary>
    public CloudEventAioAssetRef AioAssetRef { get => _aioAssetRef; init => _aioAssetRef = value ?? CloudEventAioAssetRef.Automatic; }
}
