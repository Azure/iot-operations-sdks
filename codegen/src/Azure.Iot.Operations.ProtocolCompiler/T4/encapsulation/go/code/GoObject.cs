namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class GoObject : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly ObjectType objectType;
        private IReadOnlyCollection<string> schemaImports;

        public GoObject(CodeName genNamespace, ObjectType objectType, IReadOnlyCollection<string> schemaImports)
        {
            this.genNamespace = genNamespace;
            this.objectType = objectType;
            this.schemaImports = schemaImports;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Go)}.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
