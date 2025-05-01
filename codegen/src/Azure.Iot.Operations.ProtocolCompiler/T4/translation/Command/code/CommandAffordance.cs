namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class CommandAffordance : ITemplateTransform
    {
        private readonly DTCommandInfo dtCommand;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly string commandTopic;
        private readonly string? serviceGroupId;
        private readonly bool isResponseSchemaResult;
        private readonly bool isRequestTransparent;
        private readonly bool isResponseTransparent;
        private readonly bool isCommandIdempotent;
        private readonly string? responseName;
        private readonly DTSchemaInfo? responseSchema;
        private readonly string? errorSchemaName;
        private readonly ThingDescriber thingDescriber;

        public CommandAffordance(DTCommandInfo dtCommand, int mqttVersion, bool usesTypes, string contentType, string commandTopic, string? serviceGroupId, ThingDescriber thingDescriber)
        {
            this.dtCommand = dtCommand;
            this.usesTypes = usesTypes;
            this.contentType = contentType;
            this.commandTopic = commandTopic;
            this.serviceGroupId = serviceGroupId;

            this.isResponseSchemaResult = dtCommand.Response?.Schema != null && dtCommand.Response.Schema.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ResultAdjunctTypeFormat, mqttVersion)));
            DTFieldInfo? normalField = (dtCommand.Response?.Schema as DTObjectInfo)?.Fields?.FirstOrDefault(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.NormalResultAdjunctTypeFormat, mqttVersion))));
            DTFieldInfo? errorField = (dtCommand.Response?.Schema as DTObjectInfo)?.Fields?.FirstOrDefault(f => f.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorResultAdjunctTypeFormat, mqttVersion))));

            this.isRequestTransparent = dtCommand.Request != null && dtCommand.Request.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.TransparentAdjunctTypeFormat, mqttVersion)));
            this.isResponseTransparent = !isResponseSchemaResult && dtCommand.Response != null && dtCommand.Response.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.TransparentAdjunctTypeFormat, mqttVersion)));
            this.isCommandIdempotent = dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IdempotentAdjunctTypeFormat, mqttVersion)));

            this.responseName = isResponseSchemaResult ? normalField?.Name : dtCommand.Response?.Name;
            this.responseSchema = isResponseSchemaResult ? normalField?.Schema : dtCommand.Response?.Schema;
            this.errorSchemaName = errorField != null ? new CodeName(errorField.Schema.Id).AsGiven : null;

            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
