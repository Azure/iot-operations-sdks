namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;

    public partial class DotNetIntegerEnum : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly EnumType enumType;

        public DotNetIntegerEnum(string projectName, string genNamespace, EnumType enumType)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName}.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
