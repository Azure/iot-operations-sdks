namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;
    using DTDLParser.Models;

    public partial class ObjectProto3 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName schema;
        private readonly List<(string, string, DTSchemaInfo, bool, bool, int)> nameDescSchemaIndirectRequiredIndices;
        private readonly HashSet<DTSchemaInfo> uniqueSchemas;
        private readonly HashSet<string> importNames;

        public ObjectProto3(string projectName, CodeName genNamespace, CodeName schema, List<(string, string, DTSchemaInfo, bool, bool, int)> nameDescSchemaIndirectRequiredIndices)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaIndirectRequiredIndices = nameDescSchemaIndirectRequiredIndices;
            this.uniqueSchemas = new HashSet<DTSchemaInfo>(nameDescSchemaIndirectRequiredIndices.Select(ndsi => ndsi.Item3));
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.proto"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
