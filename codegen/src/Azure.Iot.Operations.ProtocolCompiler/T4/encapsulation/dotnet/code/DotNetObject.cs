namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public partial class DotNetObject : ITemplateTransform
    {
        private readonly string projectName;
        private readonly ObjectType objectType;
        private readonly HashSet<CodeName> referencedNamespaces;
        private readonly SerializationFormat serFormat;
        private readonly bool needsNullCheck;

        public DotNetObject(string projectName, ObjectType objectType, SerializationFormat serFormat)
        {
            this.projectName = projectName;
            this.objectType = objectType;
            this.referencedNamespaces = new(TypeGeneratorSupport.GetReferencedSchemas(objectType).Select(s => s.Namespace).Where(n => !n.Equals(objectType.Namespace)));
            this.serFormat = serFormat;
            this.needsNullCheck = objectType.FieldInfos.Any(fi => fi.Value.IsRequired && DotNetSchemaSupport.IsNullable(fi.Value.SchemaType));
        }

        public string FileName { get => $"{this.objectType.SchemaName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.objectType.Namespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
