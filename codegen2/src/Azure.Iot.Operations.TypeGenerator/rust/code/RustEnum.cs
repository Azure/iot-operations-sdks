namespace Azure.Iot.Operations.TypeGenerator
{
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustEnum : ITypeTemplateTransform
    {
        private readonly EnumType enumType;
        private readonly string srcSubdir;
        private readonly bool hasNonPascalNames;

        internal RustEnum(EnumType enumType, string srcSubdir)
        {
            this.enumType = enumType;
            this.srcSubdir = srcSubdir;
            this.hasNonPascalNames = this.enumType.EnumValues.Any(v => !char.IsUpper(v.AsGiven[0]));
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.enumType.Namespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
