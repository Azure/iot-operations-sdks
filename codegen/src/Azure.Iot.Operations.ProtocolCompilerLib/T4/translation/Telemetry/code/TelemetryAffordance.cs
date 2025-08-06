namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class TelemetryAffordance : ITemplateTransform
    {
        private readonly DTTelemetryInfo dtTelemetry;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly string telemetryTopic;
        private readonly string? serviceGroupId;
        private readonly ThingDescriber thingDescriber;

        public TelemetryAffordance(DTTelemetryInfo dtTelemetry, bool usesTypes, string contentType, string telemetryTopic, string? serviceGroupId, ThingDescriber thingDescriber)
        {
            this.dtTelemetry = dtTelemetry;
            this.usesTypes = usesTypes;
            this.contentType = contentType;
            this.telemetryTopic = telemetryTopic;
            this.serviceGroupId = serviceGroupId;

            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
