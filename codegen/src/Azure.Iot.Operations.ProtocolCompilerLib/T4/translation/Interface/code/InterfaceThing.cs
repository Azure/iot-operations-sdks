namespace Azure.Iot.Operations.ProtocolCompilerLib
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
        private readonly string? propertyTopic;
        private readonly string? telemServiceGroupId;
        private readonly string? cmdServiceGroupId;
        private readonly bool aggregateTelemetries;
        private readonly bool aggregateProperties;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly Dictionary<string, DTSchemaInfo> errorSchemas;
        private readonly List<DTEnumValueInfo> enumValues;
        private readonly ThingDescriber thingDescriber;

        public InterfaceThing(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, Dtmi interfaceId, int mqttVersion)
        {
            this.dtInterface = (DTInterfaceInfo)modelDict[interfaceId];
            this.serviceName = new CodeName(dtInterface.Id);
            this.mqttVersion = mqttVersion;

            this.telemetryTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemTopicPropertyFormat, mqttVersion), out object? telemTopicObj) ? (string)telemTopicObj : null;
            this.commandTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdReqTopicPropertyFormat, mqttVersion), out object? cmdTopicObj) ? (string)cmdTopicObj : null;
            this.propertyTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.PropTopicPropertyFormat, mqttVersion), out object? propTopicObj) ? (string)propTopicObj : null;

            this.telemServiceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemServiceGroupIdPropertyFormat, mqttVersion), out object? telemServiceGroupIdObj) ? (string)telemServiceGroupIdObj : null;
            this.cmdServiceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdServiceGroupIdPropertyFormat, mqttVersion), out object? cmdServiceGroupIdObj) ? (string)cmdServiceGroupIdObj : null;

            this.aggregateTelemetries = this.telemetryTopic != null && !this.telemetryTopic.Contains(MqttTopicTokens.TelemetryName);
            this.aggregateProperties = this.propertyTopic != null && !this.propertyTopic.Contains(MqttTopicTokens.PropertyName);

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
            Dtmi errorResultType = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorResultAdjunctTypeFormat, mqttVersion));
            Dtmi readErrorType = new Dtmi(string.Format(DtdlMqttExtensionValues.ReadErrorAdjunctTypeFormat, mqttVersion));
            Dtmi writeErrorType = new Dtmi(string.Format(DtdlMqttExtensionValues.WriteErrorAdjunctTypeFormat, mqttVersion));
            Dtmi errorInfoType = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorInfoAdjunctTypeFormat, mqttVersion));

            IEnumerable<DTFieldInfo> errorFields = modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Field).Select(e => (DTFieldInfo)e).Where(f => f.SupplementalTypes.Contains(errorResultType) || f.SupplementalTypes.Contains(readErrorType) || f.SupplementalTypes.Contains(writeErrorType) || f.SupplementalTypes.Contains(errorInfoType));

            this.errorSchemas = new Dictionary<string, DTSchemaInfo>();
            foreach (DTFieldInfo errorField in errorFields)
            {
                this.errorSchemas[new CodeName(errorField.Schema.Id).AsGiven] = errorField.Schema;
            }

            this.enumValues = modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Enum).Select(e => (DTEnumInfo)e).Where(e => e.ValueSchema.Id.AbsoluteUri == "dtmi:dtdl:instance:Schema:integer;2").SelectMany(e => e.EnumValues).ToList();

            this.thingDescriber = new ThingDescriber(mqttVersion);
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.Independent)}.TD.json"; }

        public string FolderPath { get => string.Empty; }
    }
}
