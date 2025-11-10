namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    internal class RustTypeGenerator : ITypeGenerator
    {
        public TargetLanguage TargetLanguage { get => TargetLanguage.Rust; }

        public GeneratedItem GenerateTypeFromSchema(SchemaType schemaType, string projectName, CodeName genNamespace, SerializationFormat serFormat, string srcSubdir)
        {
            ITypeTemplateTransform templateTransform = schemaType switch
            {
                AliasType aliasType => new RustAlias(genNamespace, aliasType, srcSubdir),
                ObjectType objectType => new RustObject(genNamespace, objectType, allowSkipping: serFormat == SerializationFormat.Json, srcSubdir),
                EnumType enumType => new RustEnum(genNamespace, enumType, srcSubdir),
                _ => throw new Exception("unrecognized schema type"),
            };

            return new GeneratedItem(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
        }
    }
}
