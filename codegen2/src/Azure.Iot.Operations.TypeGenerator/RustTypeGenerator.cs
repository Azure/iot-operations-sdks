namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    internal class RustTypeGenerator : ITypeGenerator
    {
        public GeneratedType GenerateTypeFromSchema(SchemaType schemaType, string projectName, SerializationFormat serFormat)
        {
            ITypeTemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new RustObject(objectType, allowSkipping: serFormat == SerializationFormat.Json),
                EnumType enumType => new RustEnum(enumType),
                _ => throw new Exception("unrecognized schema type"),
            };

            return new GeneratedType(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
        }
    }
}
