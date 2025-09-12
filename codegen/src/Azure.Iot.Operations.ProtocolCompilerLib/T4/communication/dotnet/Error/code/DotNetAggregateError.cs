namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class DotNetAggregateError : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName schemaName;
        private readonly CodeName schemaNamespace;
        private readonly List<(CodeName, CodeName)> innerNameSchemas;

        public DotNetAggregateError(string projectName, CodeName schemaName, CodeName schemaNamespace, List<(CodeName, CodeName)> innerNameSchemas)
        {
            this.projectName = projectName;
            this.schemaName = schemaName;
            this.schemaNamespace = schemaNamespace;
            this.innerNameSchemas = innerNameSchemas;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.CSharp, "exception")}.g.cs"; }

        public string FolderPath { get => this.schemaNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
