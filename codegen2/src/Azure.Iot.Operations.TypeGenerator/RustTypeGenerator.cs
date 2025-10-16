namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    internal class RustTypeGenerator : ITypeGenerator
    {
        public GeneratedItem GenerateTypeFromSchema(SchemaType schemaType, string projectName, SerializationFormat serFormat, string srcSubdir)
        {
            ITypeTemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new RustObject(objectType, allowSkipping: serFormat == SerializationFormat.Json, srcSubdir),
                EnumType enumType => new RustEnum(enumType, srcSubdir),
                _ => throw new Exception("unrecognized schema type"),
            };

            return new GeneratedItem(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
        }
    }
}
