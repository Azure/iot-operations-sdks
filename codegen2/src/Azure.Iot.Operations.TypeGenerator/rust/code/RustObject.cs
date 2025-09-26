namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustObject : ITypeTemplateTransform
    {
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<ReferenceType> referencedSchemas;
        private readonly bool allowSkipping;

        internal RustObject(ObjectType objectType, bool allowSkipping)
        {
            this.objectType = objectType;
            this.referencedSchemas = TypeGeneratorSupport.GetReferencedSchemas(objectType);
            this.allowSkipping = allowSkipping;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.objectType.Namespace.GetFolderName(TargetLanguage.Rust); }
    }
}
