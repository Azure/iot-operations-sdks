// cSpell:disable
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class MqttTopicProcessorTests
    {
        [Fact]
        public void InvalidReplacementsAreInvalid()
        {
            Assert.False(MqttTopicProcessor.IsValidReplacement(null));
            Assert.False(MqttTopicProcessor.IsValidReplacement(string.Empty));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello there"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello\tthere"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello\nthere"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello/thére"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello+there"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello#there"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("{hello"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello}"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello{there}"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("/"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("//"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("/hello"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello/"));
            Assert.False(MqttTopicProcessor.IsValidReplacement("hello//there"));
        }

        [Fact]
        public void ValidReplacementsAreValid()
        {
            Assert.True(MqttTopicProcessor.IsValidReplacement("hello"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("Hello"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("HELLO"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("hello/there"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("hello/my/friend"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("!\"$%&'()*,-."));
            Assert.True(MqttTopicProcessor.IsValidReplacement(":;<=>?@"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("[\\]^_`"));
            Assert.True(MqttTopicProcessor.IsValidReplacement("|~"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidTopicPatternDoesNotValidate(bool requireReplacement)
        {
            string errMsg;
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern(null, null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern(string.Empty, null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello there", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello\tthere", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello\nthere", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/thére", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello+there", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello#there", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("{hello", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello}", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello{there}", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("/", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("//", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("/hello", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello//there", null, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("$hello", null, null, requireReplacement, out errMsg));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ValidTopicPatternValidates(bool requireReplacement)
        {
            string errMsg;
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("Hello", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("HELLO", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/there", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/my/friend", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("!\"$%&'()*,-.", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern(":;<=>?@", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("[\\]^_`", null, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("|~", null, null, requireReplacement, out errMsg));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidFirstReplacementDoesNotValidate(bool requireReplacement)
        {
            string errMsg;
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", null! } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", string.Empty } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello there" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello\tthere" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello\nthere" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello@thére" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello+there" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello#there" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "{commandName}" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "{executorId}" } }, null, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "{invokerClientId}" } }, null, requireReplacement, out errMsg));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidSecondReplacementDoesNotValidate(bool requireReplacement)
        {
            string errMsg;
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", null! } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", string.Empty } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello there" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello\tthere" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello\nthere" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello@thére" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello+there" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello#there" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "{commandName}" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "{executorId}" } }, requireReplacement, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "{invokerClientId}" } }, requireReplacement, out errMsg));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ValidReplacementValidates(bool requireReplacement)
        {
            string errMsg;
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello@there" } }, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello@there" } }, requireReplacement, out errMsg));

            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello/there" } }, null, requireReplacement, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, new Dictionary<string, string> { { "myToken", "hello/there" } }, requireReplacement, out errMsg));
        }

        [Fact]
        public void MissingReplacementDoesNotValidateIfRequired()
        {
            string errMsg;
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{foobar}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{my:hiya}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{name}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{telemetryName}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{senderId}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{name}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{commandName}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{executorId}/there", null, null, requireReplacement: true, out errMsg));
            Assert.False(MqttTopicProcessor.TryValidateTopicPattern("hello/{invokerClientId}/there", null, null, requireReplacement: true, out errMsg));
        }

        [Fact]
        public void MissingReplacementValidatesIfNotRequired()
        {
            string errMsg;
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{myToken}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{foobar}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{my:hiya}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{name}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{telemetryName}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{senderId}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{name}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{commandName}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{executorId}/there", null, null, requireReplacement: false, out errMsg));
            Assert.True(MqttTopicProcessor.TryValidateTopicPattern("hello/{invokerClientId}/there", null, null, requireReplacement: false, out errMsg));
        }

        [Fact]
        public void ResolveTopicResolvesCorrectly()
        {
            Assert.Equal("s1/reboot/svc/from/me", MqttTopicProcessor.ResolveTopic(
                "{modelId}/{commandName}/{executorId}/from/{invokerClientId}",
                new Dictionary<string, string>
                {
                    { "commandName", "reboot" },
                    { "executorId", "svc" },
                    { "invokerClientId", "me" },
                    { "modelId", "s1" },
                },
                null));

            Assert.Equal("s1/reboot/svc/from/me", MqttTopicProcessor.ResolveTopic(
                "{modelId}/{commandName}/{executorId}/from/{invokerClientId}",
                null,
                new Dictionary<string, string>
                {
                    { "commandName", "reboot" },
                    { "executorId", "svc" },
                    { "invokerClientId", "me" },
                    { "modelId", "s1" },
                }));

            Assert.Equal("+/+/+/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}"));
            Assert.Equal("+/reboot/+/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "commandName", "reboot" } }));
            Assert.Equal("+/+/svc/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "executorId", "svc" } }));
            Assert.Equal("+/+/+/from/me", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "invokerClientId", "me" } }));
            Assert.Equal("s1/+/+/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "modelId", "s1" } }));
        }
    }
}
