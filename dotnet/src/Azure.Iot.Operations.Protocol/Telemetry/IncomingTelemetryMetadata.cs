using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Azure.Iot.Operations.Protocol.Telemetry;

/// <summary>
/// The metadata associated with every message received by a <see cref="TelemetryReceiver{T}"/>.
/// </summary>
/// <remarks>
/// Some metadata should be expected if it was sent by a <see cref="TelemetrySender{T}"/> but may not be 
/// present if the message was sent by something else.
/// </remarks>
public class IncomingTelemetryMetadata
{

    /// <summary>
    /// A timestamp attached to the telemetry message.
    /// </summary>
    /// <remarks>
    /// This value is nullable only because a received message may not have sent it. Any message sent by 
    /// <see cref="TelemetrySender{T}"/> will include a non-null timestamp. A message sent by anything else
    /// may or may not include this timestamp.
    /// </remarks>
    public HybridLogicalClock? Timestamp { get; }

    /// <summary>
    /// A dictionary of user properties that are sent along with the telemetry message from the TelemetrySender.
    /// </summary>
    public Dictionary<string, string> UserData { get; }

    /// <summary>
    /// The Id of the received MQTT packet. This value can be used to acknowledge a received message via 
    /// <see cref="TelemetryReceiver{T}.AcknowledgeAsync(uint)"/>.
    /// </summary>
    public uint PacketId { get; }

    /// <summary>
    /// Provides metadata about the CloudEvents header in the message.
    /// </summary>
    public CloudEvent? CloudEvent { get; internal set; }

    /// <summary>
    /// The MQTT client Id of the client that sent this telemetry.
    /// </summary>
    /// <remarks>
    /// This value is null if the received telemetry did not include the <see cref="AkriSystemProperties.SourceId"/> header.
    /// </remarks>
    public string? SenderId { get; internal set; }

    internal IncomingTelemetryMetadata(MqttApplicationMessage message, uint packetId)
    {
        UserData = [];

        if (message.UserProperties != null)
        {
            CloudEvent = ParseCloudEventsFromMessageProperties(message.UserProperties);
            foreach (MqttUserProperty property in message.UserProperties)
            {
                switch (property.Name)
                {
                    case AkriSystemProperties.Timestamp:
                        Timestamp = HybridLogicalClock.DecodeFromString(AkriSystemProperties.Timestamp, property.Value);
                        break;
                    case AkriSystemProperties.SourceId:
                        SenderId = property.Value;
                        break;
                    default:
                        if (!property.Name.StartsWith(AkriSystemProperties.ReservedPrefix, StringComparison.InvariantCulture))
                        {
                            UserData[property.Name] = property.Value;
                        }
                        break;
                }
            }
        }

        PacketId = packetId;
    }

    private CloudEvent? ParseCloudEventsFromMessageProperties(List<MqttUserProperty> userProperties)
    {

        string safeGetUserProperty(string name) 
            => userProperties.FirstOrDefault(
                p => p.Name.Equals(name.ToLowerInvariant(), 
                StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        string specVersion = safeGetUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant());
       

        if (specVersion == "1.0")
        {
            string id = safeGetUserProperty(nameof(CloudEvent.Id));
            if (string.IsNullOrEmpty(id))
            {
                return null; // cloud events must have an id
            }

            Uri? source;
            string sourceValue = safeGetUserProperty(nameof(CloudEvent.Source));
            if (!Uri.TryCreate(sourceValue, UriKind.RelativeOrAbsolute, out source))
            {
                return null; // source must be a URI-Reference
            }
            string type = safeGetUserProperty(nameof(CloudEvent.Type));
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            string subject = safeGetUserProperty(nameof(CloudEvent.Subject));
            string dataSchema = safeGetUserProperty(nameof(CloudEvent.DataSchema));
            string dataContentType = safeGetUserProperty(nameof(CloudEvent.DataContentType));
            

            string time = safeGetUserProperty(nameof(CloudEvent.Time));
            DateTime _dateTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(time) && !DateTime.TryParse(time, CultureInfo.InvariantCulture, out _dateTime))
            {
                return null; // time must be a valid RFC3339 date-time
            }

            return new CloudEvent(source, type)
            {
                Id = id,
                Time = _dateTime,
                DataContentType = dataContentType,
                DataSchema = dataSchema,
                Subject = subject,
            };
        }
        else
        {
            return null!;
        }
    }
}
