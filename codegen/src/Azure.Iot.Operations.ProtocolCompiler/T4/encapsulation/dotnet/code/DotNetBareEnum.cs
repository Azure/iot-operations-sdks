namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class DotNetBareEnum : ITemplateTransform
    {
        private readonly string projectName;
        private readonly EnumType enumType;

        public DotNetBareEnum(string projectName, EnumType enumType)
        {
            this.projectName = projectName;
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.enumType.Namespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
