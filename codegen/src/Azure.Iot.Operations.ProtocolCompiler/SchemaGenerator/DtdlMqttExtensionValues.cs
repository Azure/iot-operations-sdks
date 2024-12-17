namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Text.RegularExpressions;
    using DTDLParser;

    public static class DtdlMqttExtensionValues
    {
        public static string MqttAdjunctTypePattern = @"dtmi:dtdl:extension:mqtt:v(\d+):Mqtt";

        public static Regex MqttAdjunctTypeRegex = new Regex(MqttAdjunctTypePattern, RegexOptions.Compiled);

        public static string RequiredAdjunctTypePattern = @"dtmi:dtdl:extension:requirement:v(\d+):Required";

        public static Regex RequiredAdjunctTypeRegex = new Regex(RequiredAdjunctTypePattern, RegexOptions.Compiled);

        public static readonly string IdempotentAdjunctTypeFormat = "dtmi:dtdl:extension:mqtt:v{0}:Idempotent";

        public static readonly string CacheableAdjunctTypeFormat = "dtmi:dtdl:extension:mqtt:v{0}:Cacheable";

        public static readonly string TransparentAdjunctTypeFormat = "dtmi:dtdl:extension:mqtt:v{0}:Transparent";

        public static readonly string IndexedAdjunctTypeFormat = "dtmi:dtdl:extension:mqtt:v{0}:Indexed";

        public static readonly string TelemTopicPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Mqtt:telemetryTopic";

        public static readonly string CmdReqTopicPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Mqtt:commandTopic";

        public static readonly string TelemServiceGroupIdPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Mqtt:telemServiceGroupId";

        public static readonly string CmdServiceGroupIdPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Mqtt:cmdServiceGroupId";

        public static readonly string IndexPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Indexed:index";

        public static readonly string PayloadFormatPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Mqtt:payloadFormat";

        public static readonly string TtlPropertyFormat = "dtmi:dtdl:extension:mqtt:v{0}:Cacheable:ttl";

        public static string GetStandardTerm(string dtmi) => dtmi.Substring(dtmi.LastIndexOf(':') + 1);
    }
}
