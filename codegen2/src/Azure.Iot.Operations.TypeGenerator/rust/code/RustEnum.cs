namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustEnum : ITypeTemplateTransform
    {
        private readonly EnumType enumType;

        internal RustEnum(EnumType enumType)
        {
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.enumType.Namespace.GetFolderName(TargetLanguage.Rust); }
    }
}
