namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class RustBareEnum : ITemplateTransform
    {
        private readonly EnumType enumType;

        public RustBareEnum(EnumType enumType)
        {
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.enumType.Namespace.GetFolderName(TargetLanguage.Rust); }
    }
}
