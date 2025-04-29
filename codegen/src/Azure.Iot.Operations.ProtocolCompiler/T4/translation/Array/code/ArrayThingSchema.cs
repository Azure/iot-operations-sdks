namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class ArrayThingSchema : ITemplateTransform
    {
        private readonly DTArrayInfo dtArray;
        private readonly int indent;
        private readonly ThingDescriber thingDescriber;

        public ArrayThingSchema(DTArrayInfo dtArray, int indent, ThingDescriber thingDescriber)
        {
            this.dtArray = dtArray;
            this.indent = indent;
            this.thingDescriber = thingDescriber;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
