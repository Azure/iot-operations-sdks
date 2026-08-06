// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class MqttPacketSizeEstimatorTests
{
    [Fact]
    public void EstimatePublishSize_MinimalQoS0Message_MatchesHandCalculation()
    {
        var message = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);

        // 1 packet type + 1 remaining length
        //   + 2 topic length prefix + 3 topic
        //   + 1 property length (zero properties)
        //   + 0 payload
        Assert.Equal(8, MqttPacketSizeEstimator.EstimatePublishSize(message));
    }

    [Fact]
    public void EstimatePublishSize_QoS1_AddsThePacketIdentifier()
    {
        var atMostOnce = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var atLeastOnce = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtLeastOnce);

        Assert.Equal(
            MqttPacketSizeEstimator.EstimatePublishSize(atMostOnce) + 2,
            MqttPacketSizeEstimator.EstimatePublishSize(atLeastOnce));
    }

    [Fact]
    public void EstimatePublishSize_CountsThePayload()
    {
        var empty = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var withPayload = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            Payload = new ReadOnlySequence<byte>(new byte[100]),
        };

        Assert.Equal(
            MqttPacketSizeEstimator.EstimatePublishSize(empty) + 100,
            MqttPacketSizeEstimator.EstimatePublishSize(withPayload));
    }

    [Fact]
    public void EstimatePublishSize_CountsEachUserProperty()
    {
        var without = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce);
        var with = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            UserProperties = [new MqttUserProperty("name", "value")],
        };

        // identifier + 2 length prefix + 4 name + 2 length prefix + 5 value
        Assert.Equal(
            MqttPacketSizeEstimator.EstimatePublishSize(without) + 14,
            MqttPacketSizeEstimator.EstimatePublishSize(with));
    }

    [Fact]
    public void EstimatePublishSize_UsesUtf8ByteCountNotCharCount()
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
            MqttPacketSizeEstimator.EstimatePublishSize(ascii) + 3,
            MqttPacketSizeEstimator.EstimatePublishSize(multiByte));
    }

    [Theory]
    [InlineData(1, 2)]        // remaining length still fits in one byte
    [InlineData(200, 3)]      // crosses into a two byte remaining length
    [InlineData(20_000, 4)]   // and then three
    public void EstimatePublishSize_AccountsForVariableByteIntegerGrowth(int payloadSize, int expectedFixedHeaderBytes)
    {
        var message = new MqttApplicationMessage("a/b", MqttQualityOfServiceLevel.AtMostOnce)
        {
            Payload = new ReadOnlySequence<byte>(new byte[payloadSize]),
        };

        // topic (2 + 3) + property length (1) + payload, plus the fixed header under test.
        long expected = expectedFixedHeaderBytes + 5 + 1 + payloadSize;

        Assert.Equal(expected, MqttPacketSizeEstimator.EstimatePublishSize(message));
    }

    [Fact]
    public void EstimatePublishSize_CountsTheOptionalPublishProperties()
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
            MqttPacketSizeEstimator.EstimatePublishSize(bare) + 54,
            MqttPacketSizeEstimator.EstimatePublishSize(full));
    }

    [Fact]
    public void EstimatePublishSize_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MqttPacketSizeEstimator.EstimatePublishSize(null!));
    }
}
