namespace Akri.Dtdl.Codegen
{
    using DTDLParser.Models;

    public partial class ArrayAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo elementSchema;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly int indent;

        public ArrayAvroSchema(DTSchemaInfo elementSchema, DtmiToSchemaName dtmiToSchemaName, int indent)
        {
            this.elementSchema = elementSchema;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
