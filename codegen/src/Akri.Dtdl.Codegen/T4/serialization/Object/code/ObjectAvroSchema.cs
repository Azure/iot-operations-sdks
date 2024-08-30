namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class ObjectAvroSchema : ITemplateTransform
    {
        private readonly string? schema;
        private readonly List<(string, DTSchemaInfo)> nameSchemas;
        private readonly DtmiToSchemaName dtmiToSchemaName;
        private readonly int indent;

        public ObjectAvroSchema(string? schema, List<(string, DTSchemaInfo)> nameSchemas, DtmiToSchemaName dtmiToSchemaName, int indent)
        {
            this.schema = schema;
            this.nameSchemas = nameSchemas;
            this.dtmiToSchemaName = dtmiToSchemaName;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
