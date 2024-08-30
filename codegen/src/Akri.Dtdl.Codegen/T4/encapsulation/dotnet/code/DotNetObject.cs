namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;

    public partial class DotNetObject : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly ObjectType objectType;

        public DotNetObject(string projectName, string genNamespace, ObjectType objectType)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.objectType = objectType;
        }

        public string FileName { get => $"{this.objectType.SchemaName}.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
