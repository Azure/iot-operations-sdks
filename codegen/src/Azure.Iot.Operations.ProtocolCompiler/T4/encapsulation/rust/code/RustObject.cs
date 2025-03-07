namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class RustObject : ITemplateTransform
    {
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<ReferenceType> referencedSchemas;
        private readonly bool allowSkipping;

        public RustObject(ObjectType objectType, bool allowSkipping)
        {
            this.objectType = objectType;
            this.referencedSchemas = TypeGeneratorSupport.GetReferencedSchemas(objectType);
            this.allowSkipping = allowSkipping;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.objectType.Namespace.GetFolderName(TargetLanguage.Rust); }
    }
}
