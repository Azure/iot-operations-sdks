namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser.Models;

    public partial class MapProto3 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName schema;
        private readonly DTSchemaInfo mapValueSchema;
        private readonly HashSet<string> importNames;

        public MapProto3(string projectName, CodeName genNamespace, CodeName schema, DTSchemaInfo mapValueSchema)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.mapValueSchema = mapValueSchema;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.proto"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
