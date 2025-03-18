namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class GoObject : ITemplateTransform
    {
        private readonly ObjectType objectType;
        private IReadOnlyCollection<string> schemaImports;

        public GoObject(ObjectType objectType, IReadOnlyCollection<string> schemaImports)
        {
            this.objectType = objectType;
            this.schemaImports = schemaImports;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Go)}.go"; }

        public string FolderPath { get => this.objectType.Namespace.GetFolderName(TargetLanguage.Go); }
    }
}
