namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class ObjectThingSchema : ITemplateTransform
    {
        private readonly IReadOnlyDictionary<string, string> objectDescription;
        private readonly List<DTFieldInfo> objectFields;
        private readonly int indent;
        private readonly int mqttVersion;
        private readonly ThingDescriber thingDescriber;
        private readonly Dtmi errorMessageAdjunctTypeId;

        public ObjectThingSchema(DTObjectInfo dtObject, int indent, int mqttVersion, ThingDescriber thingDescriber)
        {
            Dtmi errorCodeAdjunctTypeId = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorCodeAdjunctTypeFormat, mqttVersion));
            Dtmi errorInfoAdjunctTypeId = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorInfoAdjunctTypeFormat, mqttVersion));

            this.objectDescription = dtObject.Description;
            this.objectFields = dtObject.Fields.Where(f => !f.SupplementalTypes.Contains(errorCodeAdjunctTypeId) && !f.SupplementalTypes.Contains(errorInfoAdjunctTypeId)).ToList();
            this.indent = indent;
            this.mqttVersion = mqttVersion;
            this.thingDescriber = thingDescriber;
            this.errorMessageAdjunctTypeId = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorMessageAdjunctTypeFormat, mqttVersion));
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
