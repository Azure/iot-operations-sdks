// cSpell:disable
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class MqttTopicProcessorTests
    {
        private static readonly Dictionary<string, string> ReplacementMap = new()
        {
            { "foo", "good" },
            { "BAR", "QuiteGood" },
            { "Baz", "not\tso\ngood" },
            { "", "invalidKey1" },
            { "hi33", "invalidKey2" },
            { "héllo", "invalidKey3" },
            { "oh no", "invalidKey4" },
        };

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

        [Fact]
        public void InvalidCommandTopicPatternThrows()
        {
            const string paramName = "CommandParam";
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern(null, paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern(string.Empty, paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello\tthere", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello\nthere", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/thére", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello+there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello#there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("{hello", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello}", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello{there}", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("/", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("//", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("/hello", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello//there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("$hello", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{foobar}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{my:hiya}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{name}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{telemetryName}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{senderId}/there", paramName)).ParamName);
        }

        [Fact]
        public void ValidCommandTopicPatternDoesNotThrow()
        {
            const string paramName = "CommandParam";
            MqttTopicProcessor.ValidateCommandTopicPattern("hello", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("Hello", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("HELLO", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/there", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/my/friend", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("!\"$%&'()*,-.", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern(":;<=>?@", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("[\\]^_`", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("|~", paramName);

            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello@there");
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello@there");
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{executorId}/there", paramName);
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{invokerClientId}/there", paramName);
        }

        [Fact]
        public void InvalidCommandReplacementThrows()
        {
            const string paramName = "CommandParam";

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: string.Empty)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: null)).ParamName);

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello\tthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello\nthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello/there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello@thére")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello+there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "hello#there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "{commandName}")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "{executorId}")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{commandName}/there", paramName, commandName: "{invokerClientId}")).ParamName);
        }

        [Fact]
        public void InvalidCommandModelIdReplacementThrows()
        {
            const string paramName = "CommandParam";

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: string.Empty)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: null)).ParamName);

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello\tthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello\nthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello/there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello@thére")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello+there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "hello#there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "{modelId}")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "{executorId}")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{modelId}/there", paramName, modelId: "{invokerClientId}")).ParamName);
        }

        [Fact]
        public void InvalidCommandCustomReplacementThrows()
        {
            const string paramName = "CommandParam";

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:hi33}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:héllo}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:oh no}/there", paramName, customTokenMap: ReplacementMap)).ParamName);

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:foo}/there", paramName, customTokenMap: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:BAR}/there", paramName, customTokenMap: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:Baz}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:wawa}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
        }

        [Fact]
        public void ValidCommandCustomReplacementDoesNotThrow()
        {
            const string paramName = "CommandParam";
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:foo}/there", paramName, customTokenMap: ReplacementMap);
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:BAR}/there", paramName, customTokenMap: ReplacementMap);
            MqttTopicProcessor.ValidateCommandTopicPattern("hello/{ex:foo}/{ex:BAR}/there", paramName, customTokenMap: ReplacementMap);
        }

        [Fact]
        public void InvalidTelemetryTopicPatternThrows()
        {
            const string paramName = "TelemetryParam";
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern(null, paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern(string.Empty, paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello\tthere", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello\nthere", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/thére", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello+there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello#there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("{hello", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello}", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello{there}", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("/", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("//", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("/hello", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello//there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("$hello", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{foobar}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{my:hiya}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{name}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{commandName}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{executorId}/there", paramName)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{invokerClientId}/there", paramName)).ParamName);
        }

        [Fact]
        public void ValidTelemetryTopicPatternDoesNotThrow()
        {
            const string paramName = "TelemetryParam";
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("Hello", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("HELLO", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/there", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/my/friend", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("!\"$%&'()*,-.", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern(":;<=>?@", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("[\\]^_`", paramName);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("|~", paramName);

            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello@there");
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello@there");
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{senderId}/there", paramName);
        }

        [Fact]
        public void InvalidTelemetryNameReplacementThrows()
        {
            const string paramName = "TelemetryParam";

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: string.Empty)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: null)).ParamName);

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello\tthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello\nthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello/there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello@thére")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello+there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "hello#there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "{telemetryName}")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{telemetryName}/there", paramName, telemetryName: "{senderId}")).ParamName);
        }

        [Fact]
        public void InvalidTelemetryModelIdReplacementThrows()
        {
            const string paramName = "TelemetryParam";

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: string.Empty)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: null)).ParamName);

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello\tthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello\nthere")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello/there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello@thére")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello+there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "hello#there")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "{modelId}")).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{modelId}/there", paramName, modelId: "{senderId}")).ParamName);
        }

        [Fact]
        public void InvalidTelemetryCustomReplacementThrows()
        {
            const string paramName = "TelemetryParam";

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:hi33}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:héllo}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:oh no}/there", paramName, customTokenMap: ReplacementMap)).ParamName);

            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:foo}/there", paramName, customTokenMap: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:BAR}/there", paramName, customTokenMap: null)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:Baz}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
            Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:wawa}/there", paramName, customTokenMap: ReplacementMap)).ParamName);
        }

        [Fact]
        public void ValidTelemetryCustomReplacementDoesNotThrow()
        {
            const string paramName = "TelemetryParam";
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:foo}/there", paramName, customTokenMap: ReplacementMap);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:BAR}/there", paramName, customTokenMap: ReplacementMap);
            MqttTopicProcessor.ValidateTelemetryTopicPattern("hello/{ex:foo}/{ex:BAR}/there", paramName, customTokenMap: ReplacementMap);
        }

        [Fact]
        public void NullOrEmptyCommandTopicThrows()
        {
            Assert.Throws<ArgumentNullException>(() => MqttTopicProcessor.GetCommandTopic(null!));
            Assert.Throws<ArgumentException>(() => MqttTopicProcessor.GetCommandTopic(string.Empty));
        }

        [Fact]
        public void NullOrEmptyTelemetryTopicThrows()
        {
            Assert.Throws<ArgumentNullException>(() => MqttTopicProcessor.GetTelemetryTopic(null!));
            Assert.Throws<ArgumentException>(() => MqttTopicProcessor.GetTelemetryTopic(string.Empty));
        }

        [Fact]
        public void VerifyCommandTokenReplacement()
        {
            Assert.Equal("s1/reboot/svc/from/me", MqttTopicProcessor.GetCommandTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", commandName: "reboot", executorId: "svc", invokerId: "me", modelId: "s1"));
            Assert.Equal("+/+/+/from/+", MqttTopicProcessor.GetCommandTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}"));
            Assert.Equal("+/reboot/+/from/+", MqttTopicProcessor.GetCommandTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", commandName: "reboot"));
            Assert.Equal("+/+/svc/from/+", MqttTopicProcessor.GetCommandTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", executorId: "svc"));
            Assert.Equal("+/+/+/from/me", MqttTopicProcessor.GetCommandTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", invokerId: "me"));
            Assert.Equal("s1/+/+/from/+", MqttTopicProcessor.GetCommandTopic("{modelId}/{commandName}/{executorId}/from/{invokerClientId}", modelId: "s1"));
        }

        [Fact]
        public void VerifyTelemetryTokenReplacement()
        {
            Assert.Equal("s1/data/from/me", MqttTopicProcessor.GetTelemetryTopic("{modelId}/{telemetryName}/from/{senderId}", telemetryName: "data", senderId: "me", modelId: "s1"));
            Assert.Equal("+/+/from/+", MqttTopicProcessor.GetTelemetryTopic("{modelId}/{telemetryName}/from/{senderId}"));
            Assert.Equal("+/data/from/+", MqttTopicProcessor.GetTelemetryTopic("{modelId}/{telemetryName}/from/{senderId}", telemetryName: "data"));
            Assert.Equal("+/+/from/me", MqttTopicProcessor.GetTelemetryTopic("{modelId}/{telemetryName}/from/{senderId}", senderId: "me"));
            Assert.Equal("s1/+/from/+", MqttTopicProcessor.GetTelemetryTopic("{modelId}/{telemetryName}/from/{senderId}", modelId: "s1"));
        }

        [Fact]
        public void VerifyCommandCustomTokenReplacement()
        {
            Assert.Equal("s1/good/where/QuiteGood", MqttTopicProcessor.GetCommandTopic("{modelId}/{ex:foo}/where/{ex:BAR}", modelId: "s1", customTokenMap: ReplacementMap));
        }

        [Fact]
        public void VerifyTelemetryCustomTokenReplacement()
        {
            Assert.Equal("s1/good/where/QuiteGood", MqttTopicProcessor.GetTelemetryTopic("{modelId}/{ex:foo}/where/{ex:BAR}", modelId: "s1", customTokenMap: ReplacementMap));
        }
    }
}
