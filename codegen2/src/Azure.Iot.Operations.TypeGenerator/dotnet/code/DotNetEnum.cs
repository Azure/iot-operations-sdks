namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetEnum : ITypeTemplateTransform
    {
        private readonly string projectName;
        private readonly EnumType enumType;

        internal DotNetEnum(string projectName, EnumType enumType)
        {
            this.projectName = projectName;
            this.enumType = enumType;
        }

        public string FileName { get => $"{this.enumType.SchemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.enumType.Namespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
