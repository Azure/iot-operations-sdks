namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class GoObject : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly ObjectType objectType;
        private IReadOnlyCollection<string> schemaImports;

        public GoObject(string genNamespace, ObjectType objectType, IReadOnlyCollection<string> schemaImports)
        {
            this.genNamespace = genNamespace;
            this.objectType = objectType;
            this.schemaImports = schemaImports;
        }

        public string FileName { get => $"{NamingSupport.ToSnakeCase(this.objectType.SchemaName)}.go"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
