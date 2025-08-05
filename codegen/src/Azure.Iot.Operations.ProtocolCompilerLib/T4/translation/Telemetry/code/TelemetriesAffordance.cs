namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class TelemetriesAffordance : ITemplateTransform
    {
        private readonly IReadOnlyDictionary<string, DTTelemetryInfo> dtTelemetries;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly string telemetryTopic;
        private readonly string? serviceGroupId;
        private readonly string telemetryId;
        private readonly ThingDescriber thingDescriber;

        public TelemetriesAffordance(IReadOnlyDictionary<string, DTTelemetryInfo> dtTelemetries, bool usesTypes, string contentType, string telemetryTopic, string? serviceGroupId, string telemetryId, ThingDescriber thingDescriber)
        {
            this.dtTelemetries = dtTelemetries;
            this.usesTypes = usesTypes;
            this.contentType = contentType;
            this.telemetryTopic = telemetryTopic;
            this.serviceGroupId = serviceGroupId;
            this.telemetryId = telemetryId;

            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
