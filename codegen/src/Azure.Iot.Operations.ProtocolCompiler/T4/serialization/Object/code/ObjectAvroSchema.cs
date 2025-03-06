namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class ObjectAvroSchema : ITemplateTransform
    {
        private readonly CodeName schema;
        private readonly CodeName? sharedNamespace;
        private readonly List<(string, DTSchemaInfo, bool)> nameSchemaRequireds;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;

        public ObjectAvroSchema(CodeName schema, CodeName? sharedNamespace, List<(string, DTSchemaInfo, bool)> nameSchemaRequireds, int indent, CodeName? sharedPrefix)
        {
            this.schema = schema;
            this.sharedNamespace = sharedNamespace;
            this.nameSchemaRequireds = nameSchemaRequireds;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
