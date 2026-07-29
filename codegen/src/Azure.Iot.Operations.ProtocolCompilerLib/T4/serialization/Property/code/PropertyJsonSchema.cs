namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class PropertyJsonSchema : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string schemaId;
        private readonly ITypeName schema;
        private readonly List<(string, string, DTSchemaInfo, int, bool)> nameDescSchemaIndexFrags;
        private readonly CodeName? sharedPrefix;
        private readonly bool required;
        private readonly bool setIndex;

        public PropertyJsonSchema(CodeName genNamespace, string schemaId, ITypeName schema, List<(string, string, DTSchemaInfo, int, bool)> nameDescSchemaIndexFrags, CodeName? sharedPrefix, bool required, bool setIndex)
        {
            this.genNamespace = genNamespace;
            this.schemaId = schemaId;
            this.schema = schema;
            this.nameDescSchemaIndexFrags = nameDescSchemaIndexFrags;
            this.sharedPrefix = sharedPrefix;
            this.required = required;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.schema.json"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
