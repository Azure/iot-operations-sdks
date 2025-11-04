namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetAlias : ITypeTemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly AliasType aliasType;

        internal DotNetAlias(string projectName, CodeName genNamespace, AliasType aliasType)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.aliasType = aliasType;
        }

        public string FileName { get => $"{this.aliasType.SchemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
