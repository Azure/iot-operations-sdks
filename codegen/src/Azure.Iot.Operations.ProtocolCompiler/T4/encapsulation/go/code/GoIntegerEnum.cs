namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class GoIntegerEnum : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly EnumType enumType;

        public GoIntegerEnum(CodeName genNamespace, EnumType enumType)
        {
            this.genNamespace = genNamespace;
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Go)}.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
