namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class RustStringEnum : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly EnumType enumType;

        public RustStringEnum(CodeName genNamespace, EnumType enumType)
        {
            this.genNamespace = genNamespace;
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
