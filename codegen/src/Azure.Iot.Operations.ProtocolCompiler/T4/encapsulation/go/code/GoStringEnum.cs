namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class GoStringEnum : ITemplateTransform
    {
        private readonly EnumType enumType;

        public GoStringEnum(EnumType enumType)
        {
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.Go)}.go"; }

        public string FolderPath { get => this.enumType.Namespace.GetFolderName(TargetLanguage.Go); }
    }
}
