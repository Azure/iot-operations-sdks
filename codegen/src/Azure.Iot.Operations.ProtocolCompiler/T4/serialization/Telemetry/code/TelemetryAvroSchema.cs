namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryAvroSchema : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices;

        public TelemetryAvroSchema(string projectName, CodeName genNamespace, ITypeName schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.avsc"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
