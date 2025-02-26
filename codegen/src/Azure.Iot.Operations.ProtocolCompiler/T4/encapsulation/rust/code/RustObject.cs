namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class RustObject : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<CodeName> referencedSchemaNames;
        private readonly bool allowSkipping;

        public RustObject(CodeName genNamespace, ObjectType objectType, IReadOnlyCollection<CodeName> referencedSchemaNames, bool allowSkipping)
        {
            this.genNamespace = genNamespace;
            this.objectType = objectType;
            this.referencedSchemaNames = referencedSchemaNames;
            this.allowSkipping = allowSkipping;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
