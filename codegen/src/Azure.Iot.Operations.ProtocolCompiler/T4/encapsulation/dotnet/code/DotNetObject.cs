namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class DotNetObject : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly ObjectType objectType;
        private readonly SerializationFormat serFormat;
        private readonly bool needsNullCheck;

        public DotNetObject(string projectName, string genNamespace, ObjectType objectType, SerializationFormat serFormat)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.objectType = objectType;
            this.serFormat = serFormat;
            this.needsNullCheck = objectType.FieldInfos.Any(fi => fi.Value.IsRequired && DotNetSchemaSupport.IsNullable(fi.Value.SchemaType));
        }

        public string FileName { get => $"{this.objectType.SchemaName}.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
