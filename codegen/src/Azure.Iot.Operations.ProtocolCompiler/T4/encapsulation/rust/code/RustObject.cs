namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class RustObject : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly ObjectType objectType;
        private readonly IReadOnlyCollection<string> referencedSchemaNames;

        public RustObject(string genNamespace, ObjectType objectType, IReadOnlyCollection<string> referencedSchemaNames)
        {
            this.genNamespace = genNamespace;
            this.objectType = objectType;
            this.referencedSchemaNames = referencedSchemaNames;
        }

        public string FileName { get => $"{NamingSupport.ToSnakeCase(this.objectType.SchemaName)}.rs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
