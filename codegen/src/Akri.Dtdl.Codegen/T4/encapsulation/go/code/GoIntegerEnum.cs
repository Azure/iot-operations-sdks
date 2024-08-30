namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;

    public partial class GoIntegerEnum : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly EnumType enumType;

        public GoIntegerEnum(string genNamespace, EnumType enumType)
        {
            this.genNamespace = genNamespace;
            this.enumType = enumType;
        }

        public string FileName { get => $"{NamingSupport.ToSnakeCase(this.enumType.SchemaName)}.go"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
