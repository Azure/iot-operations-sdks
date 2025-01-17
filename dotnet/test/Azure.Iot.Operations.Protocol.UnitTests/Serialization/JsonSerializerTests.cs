// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.common;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class MyJsonType
    {
        public int MyIntProperty { get; set; }
        public string MyStringProperty { get; set; } = string.Empty;
        public DateTime MyDateTimeProperty { get; set; }
        public TimeSpan MyTimeSpanProperty { get; set; }
        public Guid MyGuidProperty { get; set; }
        public byte[] MyByteArrayProperty { get; set; } = Array.Empty<byte>();
        public DecimalString MyDecimalProperty { get; set; } = new();
    }

    public class JsonSerializerTests
    {
        static readonly Guid SomeGuidValue = Guid.Parse("A3EEA49F-81DC-4374-8F73-2E2125B4A1A2");
        static readonly byte[] SomeByteArray = Encoding.UTF8.GetBytes("Hello, I'm a UTF-8 string.");

        Utf8JsonSerializer ser;
        public JsonSerializerTests()
        {
            ser = new Utf8JsonSerializer();
        }

        [Fact]
        public void JsonUsesFormatIndicatorAsOne()
        {
            Assert.Equal(Models.MqttPayloadFormatIndicator.CharacterData, Utf8JsonSerializer.PayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmtpyAndNull()
        {
            byte[]? nullBytes = ser.ToBytes(new EmptyJson()).SerializedPayload;
            Assert.Null(nullBytes);
            EmptyJson? empty = ser.FromBytes<EmptyJson>(nullBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty);

            EmptyJson? empty2 = ser.FromBytes<EmptyJson>(Array.Empty<byte>(), null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty2);
        }

        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            Assert.Throws<AkriMqttException>(() => { ser.FromBytes<MyJsonType>(null, null, Models.MqttPayloadFormatIndicator.Unspecified); });
        }

        [Fact]
        public void PrimitiveTypesRoundTripWithDefaultValues()
        {
            MyJsonType myType = new();
            var bytes = ser.ToBytes(myType).SerializedPayload;
            MyJsonType fromBytes = ser.FromBytes<MyJsonType>(bytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.Equal(default, fromBytes.MyIntProperty);
            Assert.Equal("", fromBytes.MyStringProperty);
            Assert.Equal(default, fromBytes.MyDateTimeProperty);
            Assert.Equal(default, fromBytes.MyTimeSpanProperty);
            Assert.Equal(default, fromBytes.MyGuidProperty);
            Assert.Equal(Array.Empty<byte>(), fromBytes.MyByteArrayProperty);
            Assert.Equal(new DecimalString(), fromBytes.MyDecimalProperty);
        }

        [Fact]
        public void PrimitiveTypesWithCustomValues()
        {
            MyJsonType myType = new()
            {
                MyDateTimeProperty = new DateTime(2001, 02, 03),
                MyStringProperty = "my string",
                MyIntProperty = 13,
                MyTimeSpanProperty = TimeSpan.FromDays(2),
                MyGuidProperty = SomeGuidValue,
                MyByteArrayProperty = SomeByteArray,
                MyDecimalProperty = new DecimalString("55.5"),
            };
            var bytes = ser.ToBytes(myType).SerializedPayload;
            MyJsonType fromBytes = ser.FromBytes<MyJsonType>(bytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.Equal(13, fromBytes.MyIntProperty);
            Assert.Equal("my string", fromBytes.MyStringProperty);
            Assert.Equal(new DateTime(2001,02,03), fromBytes.MyDateTimeProperty);
            Assert.Equal(TimeSpan.FromDays(2), fromBytes.MyTimeSpanProperty);
            Assert.Equal(SomeGuidValue, fromBytes.MyGuidProperty);
            Assert.Equal(SomeByteArray, fromBytes.MyByteArrayProperty);
            Assert.Equal((DecimalString)"55.5", fromBytes.MyDecimalProperty);
        }

        [Fact]
        public void FromJsonString_DefaultValues()
        {
            var json = """
                        {
                            "MyIntProperty":0,
                            "MyStringProperty":"",
                            "MyDateTimeProperty":"0001-01-01T00:00:00",
                            "MyTimeSpanProperty":"PT0S",
                            "MyGuidProperty": "00000000-0000-0000-0000-000000000000",
                            "MyByteArrayProperty": "",
                            "MyDecimalProperty": "0"
                        }
                        """;
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var fromBytes = ser.FromBytes<MyJsonType>(jsonBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.Equal(default, fromBytes.MyIntProperty);
            Assert.Equal("", fromBytes.MyStringProperty);
            Assert.Equal(default, fromBytes.MyDateTimeProperty);
            Assert.Equal(default, fromBytes.MyTimeSpanProperty);
            Assert.Equal(default, fromBytes.MyGuidProperty);
            Assert.Equal(Array.Empty<byte>(), fromBytes.MyByteArrayProperty);
            Assert.Equal(new DecimalString(), fromBytes.MyDecimalProperty);
        }

        [Fact]
        public void IncompleteDocumentDeserializeToDefaults ()
        {
            var json = """
                        {
                        }
                        """;
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var fromBytes = ser.FromBytes<MyJsonType>(jsonBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.Equal(default, fromBytes.MyIntProperty);
            Assert.Equal("", fromBytes.MyStringProperty);
            Assert.Equal(default, fromBytes.MyDateTimeProperty);
            Assert.Equal(default, fromBytes.MyTimeSpanProperty);
            Assert.Equal(default, fromBytes.MyGuidProperty);
            Assert.Equal(Array.Empty<byte>(), fromBytes.MyByteArrayProperty);
            Assert.Equal(new DecimalString(), fromBytes.MyDecimalProperty);
        }
    }
}
