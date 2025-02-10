namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class RustObject : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<CodeName> referencedSchemaNames;

        public RustObject(CodeName genNamespace, ObjectType objectType, IReadOnlyCollection<CodeName> referencedSchemaNames)
        {
            this.genNamespace = genNamespace;
            this.objectType = objectType;
            this.referencedSchemaNames = referencedSchemaNames;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
