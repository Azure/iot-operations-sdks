namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class ObjectAvroSchema : ITemplateTransform
    {
        private readonly CodeName schema;
        private readonly List<(string, DTSchemaInfo, bool)> nameSchemaRequireds;
        private readonly int indent;

        public ObjectAvroSchema(CodeName schema, List<(string, DTSchemaInfo, bool)> nameSchemaRequireds, int indent)
        {
            this.schema = schema;
            this.nameSchemaRequireds = nameSchemaRequireds;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
