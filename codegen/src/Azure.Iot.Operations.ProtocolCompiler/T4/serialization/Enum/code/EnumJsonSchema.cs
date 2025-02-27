namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser;

    public partial class EnumJsonSchema : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string schemaId;
        private readonly string description;
        private readonly CodeName schema;
        private readonly string valueSchema;
        private readonly List<(string, string, int)> nameValueIndices;

        public EnumJsonSchema(CodeName genNamespace, string schemaId, string description, CodeName schema, Dtmi valueSchemaId, List<(string, string, int)> nameValueIndices)
        {
            this.genNamespace = genNamespace;
            this.schemaId = schemaId;
            this.description = description;
            this.schema = schema;
            this.valueSchema = JsonSchemaSupport.GetPrimitiveType(valueSchemaId);
            this.nameValueIndices = nameValueIndices;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.schema.json"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
