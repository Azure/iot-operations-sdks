namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    internal class DotNetTypeGenerator : ITypeGenerator
    {
        public TargetLanguage TargetLanguage { get => TargetLanguage.CSharp; }

        public GeneratedItem GenerateTypeFromSchema(SchemaType schemaType, string projectName, CodeName genNamespace, SerializationFormat serFormat, string _)
        {
            ITypeTemplateTransform templateTransform = schemaType switch
            {
                AliasType aliasType => new DotNetAlias(projectName, genNamespace, aliasType),
                ObjectType objectType => new DotNetObject(projectName, genNamespace, objectType, serFormat),
                EnumType enumType => new DotNetEnum(projectName, genNamespace, enumType),
                _ => throw new Exception("unrecognized schema type"),
            };

            return new GeneratedItem(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
        }
    }
}
