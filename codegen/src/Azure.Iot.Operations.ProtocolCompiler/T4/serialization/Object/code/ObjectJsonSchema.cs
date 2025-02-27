namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using System.Linq;
    using DTDLParser.Models;

    public partial class ObjectJsonSchema : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string schemaId;
        private readonly string description;
        private readonly CodeName schema;
        private readonly List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices;
        private readonly bool setIndex;

        public ObjectJsonSchema(CodeName genNamespace, string schemaId, string description, CodeName schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, bool setIndex)
        {
            this.genNamespace = genNamespace;
            this.schemaId = schemaId;
            this.description = description;
            this.schema = schema;
            this.nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.schema.json"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
