namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class ObjectThingSchema : ITemplateTransform
    {
        private readonly DTObjectInfo dtObject;
        private readonly int indent;
        private readonly int mqttVersion;
        private readonly ThingDescriber thingDescriber;
        private readonly Dtmi errorMessageAdjunctTypeId;

        public ObjectThingSchema(DTObjectInfo dtObject, int indent, int mqttVersion, ThingDescriber thingDescriber)
        {
            this.dtObject = dtObject;
            this.indent = indent;
            this.mqttVersion = mqttVersion;
            this.thingDescriber = thingDescriber;
            this.errorMessageAdjunctTypeId = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorMessageAdjunctTypeFormat, mqttVersion));
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
