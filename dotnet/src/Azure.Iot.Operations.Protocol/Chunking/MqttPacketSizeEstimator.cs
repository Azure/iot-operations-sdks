// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Text;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Estimates the encoded size of an MQTT 5 PUBLISH packet.
/// </summary>
/// <remarks>
/// Chunking has to decide against the size of the whole packet rather than the payload alone,
/// because user properties are unbounded and count toward the broker's limit just as much as the
/// body does. This is an estimate: the underlying client may encode a little differently, so
/// callers should keep a safety margin (see <see cref="ChunkingOptions.StaticOverhead"/>).
/// </remarks>
internal static class MqttPacketSizeEstimator
{
    private const int PacketTypeBytes = 1;
    private const int PropertyIdBytes = 1;
    private const int LengthPrefixBytes = 2;
    private const int PacketIdentifierBytes = 2;

    /// <summary>
    /// Estimates the total encoded size, in bytes, of the PUBLISH packet carrying this message.
    /// </summary>
    public static long EstimatePublishSize(MqttApplicationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        long variableHeader = LengthPrefixBytes + Utf8ByteCount(message.Topic);

        if (message.QualityOfServiceLevel != MqttQualityOfServiceLevel.AtMostOnce)
        {
            variableHeader += PacketIdentifierBytes;
        }

        long properties = EstimateProperties(message);
        variableHeader += VariableByteIntegerSize(properties) + properties;

        long remainingLength = variableHeader + message.Payload.Length;

        return PacketTypeBytes + VariableByteIntegerSize(remainingLength) + remainingLength;
    }

    private static long EstimateProperties(MqttApplicationMessage message)
    {
        long size = 0;

        if (message.PayloadFormatIndicator != MqttPayloadFormatIndicator.Unspecified)
        {
            size += PropertyIdBytes + 1;
        }

        if (message.MessageExpiryInterval != 0)
        {
            size += PropertyIdBytes + 4;
        }

        if (message.TopicAlias != 0)
        {
            size += PropertyIdBytes + 2;
        }

        if (message.CorrelationData != null)
        {
            size += PropertyIdBytes + LengthPrefixBytes + message.CorrelationData.Length;
        }

        if (!string.IsNullOrEmpty(message.ResponseTopic))
        {
            size += PropertyIdBytes + LengthPrefixBytes + Utf8ByteCount(message.ResponseTopic);
        }

        if (!string.IsNullOrEmpty(message.ContentType))
        {
            size += PropertyIdBytes + LengthPrefixBytes + Utf8ByteCount(message.ContentType);
        }

        // Subscription identifiers are set by the broker on delivery, never by a publisher, so they
        // are deliberately not counted here.

        if (message.UserProperties != null)
        {
            foreach (MqttUserProperty property in message.UserProperties)
            {
                size += PropertyIdBytes
                    + LengthPrefixBytes + Utf8ByteCount(property.Name)
                    + LengthPrefixBytes + Utf8ByteCount(property.Value);
            }
        }

        return size;
    }

    private static int VariableByteIntegerSize(long value) => value switch
    {
        < 128 => 1,
        < 16_384 => 2,
        < 2_097_152 => 3,
        _ => 4,
    };

    private static int Utf8ByteCount(string? value) =>
        value == null ? 0 : Encoding.UTF8.GetByteCount(value);
}
