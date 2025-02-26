namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class TelemetryProto2 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices;
        private readonly HashSet<DTSchemaInfo> uniqueSchemas;
        private readonly HashSet<string> importNames;

        public TelemetryProto2(string projectName, CodeName genNamespace, ITypeName schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices;
            this.uniqueSchemas = new HashSet<DTSchemaInfo>(nameDescSchemaRequiredIndices.Select(ndsi => ndsi.Item3));
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.proto"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
