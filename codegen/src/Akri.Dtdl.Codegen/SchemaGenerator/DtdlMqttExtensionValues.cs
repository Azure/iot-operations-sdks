namespace Akri.Dtdl.Codegen
{
    using DTDLParser;

    public static class DtdlMqttExtensionValues
    {
        public static readonly Dtmi MqttAdjunctType = new Dtmi("dtmi:dtdl:extension:mqtt:v1:Mqtt");

        public static readonly Dtmi IdempotentAdjunctType = new Dtmi("dtmi:dtdl:extension:mqtt:v1:Idempotent");

        public static readonly Dtmi CacheableAdjunctType = new Dtmi("dtmi:dtdl:extension:mqtt:v1:Cacheable");

        public static readonly Dtmi IndexedAdjunctType = new Dtmi("dtmi:dtdl:extension:mqtt:v1:Indexed");

        public static readonly string TelemTopicProperty = "dtmi:dtdl:extension:mqtt:v1:Mqtt:telemetryTopic";

        public static readonly string CmdReqTopicProperty = "dtmi:dtdl:extension:mqtt:v1:Mqtt:commandTopic";

        public static readonly string IndexProperty = "dtmi:dtdl:extension:mqtt:v1:Indexed:index";

        public static readonly string PayloadFormatProperty = "dtmi:dtdl:extension:mqtt:v1:Mqtt:payloadFormat";

        public static readonly string TtlProperty = "dtmi:dtdl:extension:mqtt:v1:Cacheable:ttl";

        public static string GetStandardTerm(string dtmi) => dtmi.Substring(dtmi.LastIndexOf(':') + 1);
    }
}
