namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class EnumThingSchema : ITemplateTransform
    {
        private readonly DTEnumInfo dtEnum;
        private readonly int indent;
        private readonly string valueSchema;
        private readonly bool allNamesMatchValues;

        public EnumThingSchema(DTEnumInfo dtEnum, int indent)
        {
            this.dtEnum = dtEnum;
            this.indent = indent;
            this.valueSchema = ThingDescriber.GetPrimitiveType(dtEnum.ValueSchema.Id);
            this.allNamesMatchValues = this.dtEnum.EnumValues.All(ev => ev.Name == (ev.EnumValue as string));
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
