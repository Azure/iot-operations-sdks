namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryJsonSchema : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string schemaId;
        private readonly ITypeName schema;
        private readonly List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices;
        private readonly bool setIndex;

        public TelemetryJsonSchema(CodeName genNamespace, string schemaId, ITypeName schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, bool setIndex)
        {
            this.genNamespace = genNamespace;
            this.schemaId = schemaId;
            this.schema = schema;
            this.nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.schema.json"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
