// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using Azure.Iot.Operations.Protocol.Models;
using Xunit;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class MqttUserPropertyTests
    {
        [Fact]
        public void Constructor_WithStringNameAndValue_StoresValueAsBytes()
        {
            // Arrange
            string name = "testName";
            string value = "testValue";

            // Act
            var property = new MqttUserProperty(name, value);

            // Assert
            Assert.Equal(name, property.Name);
            Assert.Equal(value, property.Value);
            Assert.False(property.ValueBuffer.IsEmpty);
            Assert.Equal(Encoding.UTF8.GetBytes(value), property.ValueBuffer.ToArray());
        }

        [Fact]
        public void Constructor_WithReadOnlyMemoryValue_StoresValueDirectly()
        {
            // Arrange
            string name = "testName";
            byte[] valueBytes = Encoding.UTF8.GetBytes("testValue");
            var valueMemory = new ReadOnlyMemory<byte>(valueBytes);

            // Act
            var property = new MqttUserProperty(name, valueMemory);

            // Assert
            Assert.Equal(name, property.Name);
            Assert.Equal("testValue", property.Value);
            Assert.Equal(valueBytes, property.ValueBuffer.ToArray());
        }

        [Fact]
        public void Constructor_WithArraySegmentValue_StoresValueCorrectly()
        {
            // Arrange
            string name = "testName";
            byte[] valueBytes = Encoding.UTF8.GetBytes("testValue");
            var arraySegment = new ArraySegment<byte>(valueBytes);

            // Act
            var property = new MqttUserProperty(name, arraySegment);

            // Assert
            Assert.Equal(name, property.Name);
            Assert.Equal("testValue", property.Value);
            Assert.Equal(valueBytes, property.ValueBuffer.ToArray());
        }

        [Fact]
        public void Constructor_WithArraySegmentOffset_StoresCorrectSubset()
        {
            // Arrange
            string name = "testName";
            byte[] fullBytes = Encoding.UTF8.GetBytes("prefixVALUEsuffix");
            // "VALUE" starts at index 6 and has length 5
            var arraySegment = new ArraySegment<byte>(fullBytes, 6, 5);

            // Act
            var property = new MqttUserProperty(name, arraySegment);

            // Assert
            Assert.Equal(name, property.Name);
            Assert.Equal("VALUE", property.Value);
        }

        [Fact]
        public void Constructor_WithNullName_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MqttUserProperty(null!, "value"));
            Assert.Throws<ArgumentNullException>(() => new MqttUserProperty(null!, new ReadOnlyMemory<byte>()));
            Assert.Throws<ArgumentNullException>(() => new MqttUserProperty(null!, new ArraySegment<byte>(new byte[1])));
        }

        [Fact]
        public void Constructor_WithDefaultArraySegment_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new MqttUserProperty("name", default(ArraySegment<byte>)));
            Assert.Contains("backing array", ex.Message);
        }

        [Fact]
        public void Constructor_WithNullStringValue_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MqttUserProperty("name", (string)null!));
        }

        [Fact]
        public void Value_WithEmptyBuffer_ReturnsEmptyString()
        {
            // Arrange
            var property = new MqttUserProperty("name", ReadOnlyMemory<byte>.Empty);

            // Act
            var value = property.Value;

            // Assert
            Assert.Equal(string.Empty, value);
        }

        [Fact]
        public void Value_WithUnicodeContent_RoundtripsCorrectly()
        {
            // Arrange
            string unicodeValue = "Hello 世界 🌍";
            
            // Act
            var property = new MqttUserProperty("name", unicodeValue);

            // Assert
            Assert.Equal(unicodeValue, property.Value);
        }

        [Fact]
        public void ReadValueAsString_ExtensionMethod_ReturnsCorrectValue()
        {
            // Arrange
            var property = new MqttUserProperty("name", "testValue");

            // Act
            var value = property.ReadValueAsString();

            // Assert
            Assert.Equal("testValue", value);
        }

        [Fact]
        public void ReadValueAsString_WithEmptyBuffer_ReturnsEmptyString()
        {
            // Arrange
            var property = new MqttUserProperty("name", ReadOnlyMemory<byte>.Empty);

            // Act
            var value = property.ReadValueAsString();

            // Assert
            Assert.Equal(string.Empty, value);
        }
    }
}
