namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustObject : ITypeTemplateTransform
    {
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<ReferenceType> referencedSchemas;
        private readonly bool allowSkipping;
        private readonly string srcSubdir;

        internal RustObject(ObjectType objectType, bool allowSkipping, string srcSubdir)
        {
            this.objectType = objectType;
            this.referencedSchemas = TypeGeneratorSupport.GetReferencedSchemas(objectType);
            this.allowSkipping = allowSkipping;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.objectType.Namespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
