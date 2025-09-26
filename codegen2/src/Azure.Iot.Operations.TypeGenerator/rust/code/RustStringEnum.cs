namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustStringEnum : ITemplateTransform
    {
        private readonly EnumType enumType;

        public RustStringEnum(EnumType enumType)
        {
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.enumType.Namespace.GetFolderName(TargetLanguage.Rust); }
    }
}
