// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class HybridLogicalClockTests
    {
        [Fact]
        public void ComparisonTests()
        {
            HybridLogicalClock hlc1 = new HybridLogicalClock(DateTime.MinValue, 0, "nodeId1");
            HybridLogicalClock hlc2 = new HybridLogicalClock(DateTime.MinValue, 0, "nodeId1");

            Assert.Equal(0, hlc1.CompareTo(hlc2));

            // Check that datetime value comparison works
            hlc1 = new HybridLogicalClock(DateTime.Now, 0, "nodeId1");
            hlc2 = new HybridLogicalClock(DateTime.MinValue, 0, "nodeId1");

            Assert.Equal(1, hlc1.CompareTo(hlc2));
            Assert.Equal(-1, hlc2.CompareTo(hlc1));
            Assert.False(hlc1.Equals(hlc2));

            // Check that counter value is the tie-breaker if date times are the same
            hlc1 = new HybridLogicalClock(DateTime.MinValue, 1, "nodeId1");
            hlc2 = new HybridLogicalClock(DateTime.MinValue, 0, "nodeId1");

            Assert.Equal(1, hlc1.CompareTo(hlc2));
            Assert.Equal(-1, hlc2.CompareTo(hlc1));
            Assert.False(hlc1.Equals(hlc2));

            // Check that date time comparison takes precedence over counter value comparison
            hlc1 = new HybridLogicalClock(DateTime.Now, 0, "nodeId1");
            hlc2 = new HybridLogicalClock(DateTime.MinValue, 1, "nodeId1");

            Assert.Equal(1, hlc1.CompareTo(hlc2));
            Assert.Equal(-1, hlc2.CompareTo(hlc1));
            Assert.False(hlc1.Equals(hlc2));

            // Check that node Id value is the tie-breaker if date times and counters are the same
            hlc1 = new HybridLogicalClock(DateTime.MinValue, 0, "nodeId2");
            hlc2 = new HybridLogicalClock(DateTime.MinValue, 0, "nodeId1");

            Assert.Equal(1, hlc1.CompareTo(hlc2));
            Assert.Equal(-1, hlc2.CompareTo(hlc1));
            Assert.False(hlc1.Equals(hlc2));
        }

        [Fact]
        public async Task UpdateTests()
        {
            string nodeId = "nodeId";
            string otherNodeId = "otherNodeId";
            DateTime now = DateTime.UtcNow;
            TimeSpan maxClockDrift = TimeSpan.MaxValue;

            HybridLogicalClock hlc1 = new HybridLogicalClock(DateTime.MinValue, 0, nodeId);
            HybridLogicalClock hlc2 = new HybridLogicalClock(DateTime.MinValue, 0, nodeId);

            // Check that an HLC won't update if it is identical to the other HLC
            await hlc1.UpdateWithOtherAsync(hlc2);

            Assert.Equal(DateTime.MinValue, hlc1.Timestamp);
            Assert.Equal(0, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);

            hlc1 = new HybridLogicalClock(DateTime.MinValue, 0, nodeId);
            hlc2 = new HybridLogicalClock(DateTime.MaxValue, 100, nodeId);

            // Check that an HLC won't update with itself (the same node Id)
            await hlc1.UpdateWithOtherAsync(hlc2);

            Assert.Equal(DateTime.MinValue, hlc1.Timestamp);
            Assert.Equal(0, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);

            hlc1 = new HybridLogicalClock(DateTime.MinValue, 0, nodeId);
            hlc2 = new HybridLogicalClock(DateTime.MaxValue, 0, nodeId);

            // Check that an HLC won't update with itself (the same node Id)
            await hlc1.UpdateWithOtherAsync(hlc2);

            Assert.Equal(DateTime.MinValue, hlc1.Timestamp);
            Assert.Equal(0, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);

            hlc1 = new HybridLogicalClock(now + TimeSpan.FromHours(1), 10, nodeId);
            hlc2 = new HybridLogicalClock(now + TimeSpan.FromHours(10), 1, otherNodeId);

            // Check that an HLC will update both timestamp and counter if they are both greater on the other HLC
            await hlc1.UpdateWithOtherAsync(hlc2, maxClockDrift);

            Assert.NotEqual(now, hlc1.Timestamp);
            Assert.Equal(2, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);

            hlc1 = new HybridLogicalClock(now + TimeSpan.FromHours(1), 1, nodeId);
            hlc2 = new HybridLogicalClock(now + TimeSpan.FromHours(1), 2, otherNodeId);

            // Check that an HLC will update counter if it is greater on the other HLC and the timestamps are identical
            await hlc1.UpdateWithOtherAsync(hlc2, maxClockDrift);

            Assert.NotEqual(now, hlc1.Timestamp);
            Assert.Equal(3, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);

            hlc1 = new HybridLogicalClock(now - TimeSpan.FromHours(1), 1, nodeId);
            hlc2 = new HybridLogicalClock(now - TimeSpan.FromHours(1), 2, otherNodeId);

            // Check that an HLC will update counter to 0 and clock to now if both HLCs have earlier timestamps
            await hlc1.UpdateWithOtherAsync(hlc2, maxClockDrift);

            Assert.Equal(-1, now.CompareTo(hlc1.Timestamp));
            Assert.Equal(0, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);

            hlc1 = new HybridLogicalClock(now + TimeSpan.FromHours(10), 10, nodeId);
            hlc2 = new HybridLogicalClock(now + TimeSpan.FromHours(1), 100, otherNodeId);

            // Check that an HLC will increment its own counter if its timestamp is greater than the other HLC's
            await hlc1.UpdateWithOtherAsync(hlc2, maxClockDrift);

            Assert.NotEqual(now, hlc1.Timestamp);
            Assert.Equal(11, hlc1.Counter);
            Assert.Equal(nodeId, hlc1.NodeId);
        }

        [Fact]
        public void EncodeFromStringSuccess()
        {
            int expectedCounter = 12;
            string expectedNodeId = Guid.NewGuid().ToString();
            HybridLogicalClock hlc = new(DateTime.MaxValue, expectedCounter, expectedNodeId);

            string actualEncodedHlc = hlc.EncodeToString();

            Assert.Equal($"253402300799998.97:000{expectedCounter}:{expectedNodeId}", actualEncodedHlc);
        }

        [Fact]
        public void DecodeFromStringSuccess()
        {
            int expectedCounter = 12;
            string expectedNodeId = Guid.NewGuid().ToString();

            string encodedHlc = $"253402300799998.97:000{expectedCounter}:{expectedNodeId}";

            HybridLogicalClock hlc = HybridLogicalClock.DecodeFromString("", encodedHlc);

            Assert.Equal(DateTime.MaxValue.Date, hlc.Timestamp.Date); // Can't compare down to millisecond level due to rounding. Just check date level
            Assert.Equal(expectedCounter, hlc.Counter);
            Assert.Equal(expectedNodeId, hlc.NodeId);
        }

        [Fact]
        public void DecodeFromStringThrowsIfMalformedString()
        {
            int expectedCounter = 12;
            string expectedNodeId = Guid.NewGuid().ToString();

            // missing node Id
            Assert.Throws<AkriMqttException>(() => HybridLogicalClock.DecodeFromString("", $"253402300799998.97:000{expectedCounter}"));

            // zero length node Id
            Assert.Throws<AkriMqttException>(() => HybridLogicalClock.DecodeFromString("", $"253402300799998.97:000{expectedCounter}:"));

            // missing counter
            Assert.Throws<AkriMqttException>(() => HybridLogicalClock.DecodeFromString("", $"253402300799998.97:{expectedNodeId}"));

            // missing timestamp
            Assert.Throws<AkriMqttException>(() => HybridLogicalClock.DecodeFromString("", $"{expectedCounter}:{expectedNodeId}"));

            // non-int counter
            Assert.Throws<AkriMqttException>(() => HybridLogicalClock.DecodeFromString("", $"253402300799998.97:{"someNonInteger"}:{expectedNodeId}"));
        }
    }
}
