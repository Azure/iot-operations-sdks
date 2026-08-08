// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Models;
using MQTTnet.Formatter;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class MqttPacketSizeCalculatorTests
{
    [Fact]
    public void CalculatePublishSize_MinimalQoS0Message_MatchesHandCalculation()
    {
        var message = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);

        // 1 packet type + 1 remaining length
        //   + 2 topic length prefix + 3 topic
        //   + 1 property length (zero properties)
        //   + 0 payload
        Assert.Equal(8, MqttPacketSizeCalculator.CalculatePublishSize(message));
    }

    [Fact]
    public void CalculatePublishSize_QoS1_AddsThePacketIdentifier()
    {
        var atMostOnce = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var atLeastOnce = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtLeastOnce);

        Assert.Equal(
            MqttPacketSizeCalculator.CalculatePublishSize(atMostOnce) + 2,
            MqttPacketSizeCalculator.CalculatePublishSize(atLeastOnce));
    }

    [Fact]
    public void CalculatePublishSize_CountsThePayload()
    {
        var empty = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var withPayload = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            Payload = new ReadOnlySequence<byte>(new byte[100]),
        };

        Assert.Equal(
            MqttPacketSizeCalculator.CalculatePublishSize(empty) + 100,
            MqttPacketSizeCalculator.CalculatePublishSize(withPayload));
    }

    [Fact]
    public void CalculatePublishSize_CountsEachUserProperty()
    {
        var without = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var with = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            UserProperties = [new MqttUserProperty("name", "value")],
        };

        // identifier + 2 length prefix + 4 name + 2 length prefix + 5 value
        Assert.Equal(
            MqttPacketSizeCalculator.CalculatePublishSize(without) + 14,
            MqttPacketSizeCalculator.CalculatePublishSize(with));
    }

    [Fact]
    public void CalculatePublishSize_UsesUtf8ByteCountNotCharCount()
    {
        var ascii = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            UserProperties = [new MqttUserProperty("n", "aaa")],
        };
        var multiByte = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            UserProperties = [new MqttUserProperty("n", "\u00e9\u00e9\u00e9")],
        };

        Assert.Equal(3, Encoding.UTF8.GetByteCount("aaa"));
        Assert.Equal(6, Encoding.UTF8.GetByteCount("\u00e9\u00e9\u00e9"));

        Assert.Equal(
            MqttPacketSizeCalculator.CalculatePublishSize(ascii) + 3,
            MqttPacketSizeCalculator.CalculatePublishSize(multiByte));
    }

    [Theory]
    [InlineData(1, 2)]        // remaining length still fits in one byte
    [InlineData(200, 3)]      // crosses into a two byte remaining length
    [InlineData(20_000, 4)]   // and then three
    public void CalculatePublishSize_AccountsForVariableByteIntegerGrowth(int payloadSize, int expectedFixedHeaderBytes)
    {
        var message = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            Payload = new ReadOnlySequence<byte>(new byte[payloadSize]),
        };

        // topic (2 + 3) + property length (1) + payload, plus the fixed header under test.
        long expected = expectedFixedHeaderBytes + 5 + 1 + payloadSize;

        Assert.Equal(expected, MqttPacketSizeCalculator.CalculatePublishSize(message));
    }

    [Fact]
    public void CalculatePublishSize_CountsTheOptionalPublishProperties()
    {
        var bare = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var full = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,  // 1 + 1
            MessageExpiryInterval = 30,                                          // 1 + 4
            TopicAlias = 7,                                                      // 1 + 2
            CorrelationData = new byte[16],                                      // 1 + 2 + 16
            ResponseTopic = "r/t",                                               // 1 + 2 + 3
            ContentType = "application/json",                                    // 1 + 2 + 16
        };

        Assert.Equal(
            MqttPacketSizeCalculator.CalculatePublishSize(bare) + 54,
            MqttPacketSizeCalculator.CalculatePublishSize(full));
    }

    [Fact]
    public void CalculatePublishSize_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MqttPacketSizeCalculator.CalculatePublishSize(null!));
    }

    // The calculation is only worth calling exact if it agrees with the encoder that actually puts
    // packets on the wire. These cases cover the places the two could plausibly disagree: the
    // omit-when-default property rules, variable byte integer boundaries, and UTF-8 width.
    public static TheoryData<string, MqttApplicationMessage> EncodingCases() => new()
    {
        { "bare QoS0", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce) },
        { "bare QoS1", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtLeastOnce) },
        { "bare QoS2", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.ExactlyOnce) },
        { "expiry omitted when zero", new MqttApplicationMessage("a/b") { MessageExpiryInterval = 0 } },
        { "expiry present", new MqttApplicationMessage("a/b") { MessageExpiryInterval = 30 } },
        { "format indicator unspecified", new MqttApplicationMessage("a/b") { PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified } },
        { "format indicator character data", new MqttApplicationMessage("a/b") { PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData } },
        { "topic alias omitted when zero", new MqttApplicationMessage("a/b") { TopicAlias = 0 } },
        { "topic alias present", new MqttApplicationMessage("a/b") { TopicAlias = 7 } },
        { "correlation data present but empty", new MqttApplicationMessage("a/b") { CorrelationData = [] } },
        { "retain", new MqttApplicationMessage("a/b") { Retain = true } },
        { "payload just below a VBI boundary", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce) { Payload = new ReadOnlySequence<byte>(new byte[119]) } },
        { "payload just above a VBI boundary", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce) { Payload = new ReadOnlySequence<byte>(new byte[120]) } },
        { "payload at the two byte VBI boundary", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce) { Payload = new ReadOnlySequence<byte>(new byte[16_378]) } },
        { "payload at the three byte VBI boundary", new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce) { Payload = new ReadOnlySequence<byte>(new byte[2_097_146]) } },
        { "multi byte topic", new MqttApplicationMessage("\u00e9/\u00e9\u00e9") },
        { "multi byte user property", new MqttApplicationMessage("a/b") { UserProperties = [new MqttUserProperty("n\u00e9me", "v\u00e1lue")] } },
        { "single subscription identifier", new MqttApplicationMessage("a/b") { SubscriptionIdentifiers = [1] } },
        { "subscription identifier spanning two VBI bytes", new MqttApplicationMessage("a/b") { SubscriptionIdentifiers = [300] } },
        { "several subscription identifiers", new MqttApplicationMessage("a/b") { SubscriptionIdentifiers = [1, 200, 40_000] } },
        {
            "many user properties",
            new MqttApplicationMessage("a/b")
            {
                UserProperties = Enumerable.Range(0, 50).Select(i => new MqttUserProperty($"name{i:D3}", new string('v', 100))).ToList(),
            }
        },
        {
            "every property at once",
            new MqttApplicationMessage("some/topic/here", MqttQualityOfServiceLevel.AtLeastOnce)
            {
                Payload = new ReadOnlySequence<byte>(new byte[1000]),
                ResponseTopic = "clients/abc/response",
                CorrelationData = new byte[16],
                MessageExpiryInterval = 30,
                ContentType = "application/json",
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                TopicAlias = 3,
                Retain = true,
                UserProperties = [new MqttUserProperty("__chunk", "d:8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11:12")],
            }
        },
    };

    [Theory]
    [MemberData(nameof(EncodingCases))]
    public void CalculatePublishSize_MatchesTheMqttClientEncoderExactly(string name, MqttApplicationMessage message)
    {
        long encoded = EncodeWithMqttNet(message);
        long calculated = MqttPacketSizeCalculator.CalculatePublishSize(message);

        Assert.True(
            encoded == calculated,
            $"'{name}': the encoder produced {encoded} bytes but the calculator said {calculated}.");
    }

    private static long EncodeWithMqttNet(MqttApplicationMessage message)
    {
        var packet = new MQTTnet.Packets.MqttPublishPacket
        {
            Topic = message.Topic,
            PayloadSegment = new ArraySegment<byte>(message.Payload.ToArray()),
            QualityOfServiceLevel = (MQTTnet.Protocol.MqttQualityOfServiceLevel)(int)message.QualityOfServiceLevel,
            PacketIdentifier = message.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce ? (ushort)0 : (ushort)1,
            ResponseTopic = message.ResponseTopic,
            CorrelationData = message.CorrelationData,
            MessageExpiryInterval = message.MessageExpiryInterval,
            ContentType = message.ContentType,
            TopicAlias = message.TopicAlias,
            Retain = message.Retain,
            PayloadFormatIndicator = (MQTTnet.Protocol.MqttPayloadFormatIndicator)(int)message.PayloadFormatIndicator,
            SubscriptionIdentifiers = message.SubscriptionIdentifiers?.ToList(),
            UserProperties = message.UserProperties?
                .Select(p => new MQTTnet.Packets.MqttUserProperty(p.Name, Encoding.UTF8.GetBytes(p.Value)))
                .ToList(),
        };

        var adapter = new MqttPacketFormatterAdapter(
            MQTTnet.Formatter.MqttProtocolVersion.V500,
            new MqttBufferWriter(4096, 10 * 1024 * 1024));

        return adapter.Encode(packet).Length;
    }
}
