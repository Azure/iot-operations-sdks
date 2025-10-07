namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class PropertyAffordance : ITemplateTransform
    {
        private readonly DTPropertyInfo dtProperty;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly bool separate;
        private readonly string propertyTopic;
        private readonly bool isSchemaPropertyResult;
        private readonly string? valueName;
        private readonly DTSchemaInfo? valueSchema;
        private readonly string? readErrorSchemaName;
        private readonly string? writeErrorSchemaName;
        private readonly ThingDescriber thingDescriber;

        public PropertyAffordance(DTPropertyInfo dtProperty, int mqttVersion, bool usesTypes, string contentType, string propertyTopic, ThingDescriber thingDescriber)
        {
            this.dtProperty = dtProperty;
            this.usesTypes = usesTypes;
            this.contentType = contentType;
            this.separate = propertyTopic.Contains(MqttTopicTokens.PropertyName);
            this.propertyTopic = propertyTopic.Replace(MqttTopicTokens.PropertyName, this.dtProperty.Name);

            this.isSchemaPropertyResult = dtProperty.Schema.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.PropertyResultAdjunctTypeFormat, mqttVersion)));
            DTFieldInfo? valueField = (dtProperty.Schema as DTObjectInfo)?.Fields?.FirstOrDefault(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.PropertyValueAdjunctTypeFormat, mqttVersion))));
            DTFieldInfo? readErrorField = (dtProperty.Schema as DTObjectInfo)?.Fields?.FirstOrDefault(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ReadErrorAdjunctTypeFormat, mqttVersion))));
            DTFieldInfo? writeErrorField = (dtProperty.Schema as DTObjectInfo)?.Fields?.FirstOrDefault(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.WriteErrorAdjunctTypeFormat, mqttVersion))));

            this.valueName = isSchemaPropertyResult ? valueField?.Name : dtProperty.Name;
            this.valueSchema = isSchemaPropertyResult ? valueField?.Schema : dtProperty.Schema;
            this.readErrorSchemaName = readErrorField != null ? new CodeName(readErrorField.Schema.Id).AsGiven : null;
            this.writeErrorSchemaName = writeErrorField != null ? new CodeName(writeErrorField.Schema.Id).AsGiven : null;

            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
