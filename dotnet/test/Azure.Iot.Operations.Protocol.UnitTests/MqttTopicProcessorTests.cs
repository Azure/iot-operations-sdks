// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern(null, null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern(string.Empty, null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello there", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello\tthere", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello\nthere", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello/thére", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello+there", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello#there", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("{hello", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello}", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello{there}", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("/", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("//", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("/hello", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello/", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("hello//there", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.InvalidPattern, MqttTopicProcessor.ValidateTopicPattern("$hello", null, requireReplacement, out _, out _, out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ValidTopicPatternValidates(bool requireReplacement)
        {
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("Hello", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("HELLO", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/there", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/my/friend", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("!\"$%&'()*,-.", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern(":;<=>?@", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("[\\]^_`", null, requireReplacement, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("|~", null, requireReplacement, out _, out _, out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidResidentReplacementDoesNotValidate(bool requireReplacement)
        {
            string? errToken;
            string? errReplacement;

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", string.Empty } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal(string.Empty, errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello there" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("hello there", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello\tthere" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("hello\tthere", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello\nthere" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("hello\nthere", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello@thére" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("hello@thére", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello+there" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("hello+there", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello#there" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("hello#there", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "{commandName}" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("{commandName}", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "{executorId}" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("{executorId}", errReplacement);

            Assert.Equal(PatternValidity.InvalidResidentReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "{invokerClientId}" } }, requireReplacement, out _, out errToken, out errReplacement));
            Assert.Equal("myToken", errToken);
            Assert.Equal("{invokerClientId}", errReplacement);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ValidReplacementValidates(bool requireReplacement)
        {
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello@there" } }, requireReplacement, out _, out _, out _));

            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", new Dictionary<string, string> { { "myToken", "hello/there" } }, requireReplacement, out _, out _, out _));
        }

        [Fact]
        public void MissingReplacementDoesNotValidateIfRequired()
        {
            string? errToken;

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("myToken", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{foobar}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("foobar", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{my:hiya}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("my:hiya", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{name}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("name", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{telemetryName}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("telemetryName", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{senderId}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("senderId", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{name}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("name", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{commandName}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("commandName", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{executorId}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("executorId", errToken);

            Assert.Equal(PatternValidity.MissingReplacement, MqttTopicProcessor.ValidateTopicPattern("hello/{invokerClientId}/there", null, requireReplacement: true, out _, out errToken, out _));
            Assert.Equal("invokerClientId", errToken);
        }

        [Fact]
        public void MissingReplacementValidatesIfNotRequired()
        {
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{myToken}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{foobar}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{my:hiya}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{name}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{telemetryName}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{senderId}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{name}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{commandName}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{executorId}/there", null, requireReplacement: false, out _, out _, out _));
            Assert.Equal(PatternValidity.Valid, MqttTopicProcessor.ValidateTopicPattern("hello/{invokerClientId}/there", null, requireReplacement: false, out _, out _, out _));
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
                }));

            Assert.Equal("+/+/+/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}"));
            Assert.Equal("+/reboot/+/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "commandName", "reboot" } }));
            Assert.Equal("+/+/svc/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "executorId", "svc" } }));
            Assert.Equal("+/+/+/from/me", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "invokerClientId", "me" } }));
            Assert.Equal("s1/+/+/from/+", MqttTopicProcessor.ResolveTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", new Dictionary<string, string> { { "modelId", "s1" } }));
        }
    }
}
