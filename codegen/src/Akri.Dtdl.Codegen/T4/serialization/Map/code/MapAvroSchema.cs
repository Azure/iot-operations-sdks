namespace Akri.Dtdl.Codegen
{
    using DTDLParser.Models;

    public partial class MapAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo valueSchema;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly int indent;

        public MapAvroSchema(DTSchemaInfo valueSchema, DtmiToSchemaName dtmiToSchemaName, int indent)
        {
            this.valueSchema = valueSchema;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
