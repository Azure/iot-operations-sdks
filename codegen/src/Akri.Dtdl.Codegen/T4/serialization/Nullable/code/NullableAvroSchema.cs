namespace Akri.Dtdl.Codegen
{
    using DTDLParser.Models;

    public partial class NullableAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo schema;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly int indent;

        public NullableAvroSchema(DTSchemaInfo schema, DtmiToSchemaName dtmiToSchemaName, int indent)
        {
            this.schema = schema;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
