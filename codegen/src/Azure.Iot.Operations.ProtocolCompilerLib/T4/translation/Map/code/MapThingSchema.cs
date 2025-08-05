namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser.Models;

    public partial class MapThingSchema : ITemplateTransform
    {
        private readonly DTMapInfo dtMap;
        private readonly int indent;
        private readonly ThingDescriber thingDescriber;

        public MapThingSchema(DTMapInfo dtMap, int indent, ThingDescriber thingDescriber)
        {
            this.dtMap = dtMap;
            this.indent = indent;
            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
