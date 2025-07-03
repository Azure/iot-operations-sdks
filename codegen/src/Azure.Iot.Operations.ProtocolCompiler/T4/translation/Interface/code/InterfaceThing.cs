namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class InterfaceThing : ITemplateTransform
    {
        private readonly DTInterfaceInfo dtInterface;
        private readonly CodeName serviceName;
        private readonly int mqttVersion;
        private readonly string? telemetryTopic;
        private readonly string? commandTopic;
        private readonly string? telemServiceGroupId;
        private readonly string? cmdServiceGroupId;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly Dictionary<string, DTSchemaInfo> errorSchemas;
        private readonly ThingDescriber thingDescriber;

        public InterfaceThing(DTInterfaceInfo dtInterface, int mqttVersion)
        {
            this.dtInterface = dtInterface;
            this.serviceName = new CodeName(dtInterface.Id);
            this.mqttVersion = mqttVersion;

            this.telemetryTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemTopicPropertyFormat, mqttVersion), out object? telemTopicObj) ? (string)telemTopicObj : null;
            this.commandTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdReqTopicPropertyFormat, mqttVersion), out object? cmdTopicObj) ? (string)cmdTopicObj : null;

            this.telemServiceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemServiceGroupIdPropertyFormat, mqttVersion), out object? telemServiceGroupIdObj) ? (string)telemServiceGroupIdObj : null;
            this.cmdServiceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdServiceGroupIdPropertyFormat, mqttVersion), out object? cmdServiceGroupIdObj) ? (string)cmdServiceGroupIdObj : null;

            string payloadFormat = (string)dtInterface.SupplementalProperties[string.Format(DtdlMqttExtensionValues.PayloadFormatPropertyFormat, mqttVersion)];
            this.usesTypes = payloadFormat != PayloadFormat.Raw && payloadFormat != PayloadFormat.Custom;
            this.contentType = payloadFormat switch
            {
                PayloadFormat.Avro => "application/avro",
                PayloadFormat.Cbor => "application/cbor",
                PayloadFormat.Json => "application/json",
                PayloadFormat.Proto2 => "application/protobuf",
                PayloadFormat.Proto3 => "application/protobuf",
                PayloadFormat.Raw => "application/octet-stream",
                PayloadFormat.Custom => "",
                _ => throw new InvalidOperationException($"InvalidOperationException \"{payloadFormat}\" not recognized"),
            };

            IEnumerable<DTFieldInfo> errorFields = dtInterface.Commands
                .Where(c => c.Value.Response?.Schema != null && c.Value.Response.Schema.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ResultAdjunctTypeFormat, mqttVersion))) &&
                    ((DTObjectInfo)c.Value.Response.Schema).Fields.Any(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorResultAdjunctTypeFormat, mqttVersion)))))
                .Select(c => ((DTObjectInfo)c.Value.Response.Schema).Fields.First(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorResultAdjunctTypeFormat, mqttVersion)))));

            this.errorSchemas = new Dictionary<string, DTSchemaInfo>();
            foreach (DTFieldInfo errorField in errorFields)
            {
                this.errorSchemas[new CodeName(errorField.Schema.Id).AsGiven] = errorField.Schema;
            }

            this.thingDescriber = new ThingDescriber(mqttVersion);
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.Independent)}.TD.json"; }

        public string FolderPath { get => string.Empty; }
    }
}
