// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Dtdl2Wot
{
    using DTDLParser.Models;

    public partial class TelemetryAffordance : ITemplateTransform
    {
        private readonly DTTelemetryInfo dtTelemetry;
        private readonly bool usesTypes;
        private readonly string contentType;
        private readonly bool separate;
        private readonly string telemetryTopic;
        private readonly string? serviceGroupId;
        private readonly ThingDescriber thingDescriber;

        public TelemetryAffordance(DTTelemetryInfo dtTelemetry, bool usesTypes, string contentType, string telemetryTopic, string? serviceGroupId, ThingDescriber thingDescriber)
        {
            this.dtTelemetry = dtTelemetry;
            this.usesTypes = usesTypes;
            this.contentType = contentType;
            this.separate = telemetryTopic.Contains(DtdlMqttTopicTokens.TelemetryName);
            this.telemetryTopic = telemetryTopic.Replace(DtdlMqttTopicTokens.TelemetryName, this.dtTelemetry.Name);
            this.serviceGroupId = serviceGroupId;

            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
