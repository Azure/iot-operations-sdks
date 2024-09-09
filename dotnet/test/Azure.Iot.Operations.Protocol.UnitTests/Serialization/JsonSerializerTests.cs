using System.Text;
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
    }

    public class JsonSerializerTests
    {
        static readonly Guid SomeGuidValue = Guid.Parse("A3EEA49F-81DC-4374-8F73-2E2125B4A1A2");

        Utf8JsonSerializer ser;
        public JsonSerializerTests()
        {
            ser = new Utf8JsonSerializer();
        }

        [Fact]
        public void JsonUsesFormatIndicatorAsOne()
        {
            Assert.Equal(1, ser.CharacterDataFormatIndicator);
        }

        [Fact]
        public void DeserializeEmtpyAndNull()
        {
            byte[]? nullBytes = ser.ToBytes(new EmptyJson());
            Assert.Null(nullBytes);
            EmptyJson? empty = ser.FromBytes<EmptyJson>(nullBytes);
            Assert.NotNull(empty);

            EmptyJson? empty2 = ser.FromBytes<EmptyJson>(Array.Empty<byte>());
            Assert.NotNull(empty2);
        }



        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            Assert.Throws<AkriMqttException>(() => { ser.FromBytes<MyJsonType>(null); });
        }

        [Fact]
        public void PrimitiveTypesRoundTripWithDefaultValues()
        {
            MyJsonType myType = new();
            var bytes = ser.ToBytes(myType);
            MyJsonType fromBytes = ser.FromBytes<MyJsonType>(bytes);
            Assert.Equal(default, fromBytes.MyIntProperty);
            Assert.Equal("", fromBytes.MyStringProperty);
            Assert.Equal(default, fromBytes.MyDateTimeProperty);
            Assert.Equal(default, fromBytes.MyTimeSpanProperty);
            Assert.Equal(default, fromBytes.MyGuidProperty);
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
            };
            var bytes = ser.ToBytes(myType);
            MyJsonType fromBytes = ser.FromBytes<MyJsonType>(bytes);
            Assert.Equal(13, fromBytes.MyIntProperty);
            Assert.Equal("my string", fromBytes.MyStringProperty);
            Assert.Equal(new DateTime(2001,02,03), fromBytes.MyDateTimeProperty);
            Assert.Equal(TimeSpan.FromDays(2), fromBytes.MyTimeSpanProperty);
            Assert.Equal(SomeGuidValue, fromBytes.MyGuidProperty);
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
                            "MyGuidProperty": "00000000-0000-0000-0000-000000000000"
                        }
                        """;
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var fromBytes = ser.FromBytes<MyJsonType>(jsonBytes);
            Assert.Equal(default, fromBytes.MyIntProperty);
            Assert.Equal("", fromBytes.MyStringProperty);
            Assert.Equal(default, fromBytes.MyDateTimeProperty);
            Assert.Equal(default, fromBytes.MyTimeSpanProperty);
            Assert.Equal(default, fromBytes.MyGuidProperty);
        }

        [Fact]
        public void IncompleteDocumentDeserializeToDefaults ()
        {
            var json = """
                        {
                        }
                        """;
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var fromBytes = ser.FromBytes<MyJsonType>(jsonBytes);
            Assert.Equal(default, fromBytes.MyIntProperty);
            Assert.Equal("", fromBytes.MyStringProperty);
            Assert.Equal(default, fromBytes.MyDateTimeProperty);
            Assert.Equal(default, fromBytes.MyTimeSpanProperty);
            Assert.Equal(default, fromBytes.MyGuidProperty);
        }
    }
}
