namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class RustTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(Action<string, string, string> acceptor, string projectName, SchemaType schemaType, SerializationFormat serFormat)
        {
            ITemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new RustObject(objectType, allowSkipping: serFormat == SerializationFormat.Json),
                EnumType enumType =>
                    enumType.EnumValues.FirstOrDefault()?.StringValue != null ? new RustStringEnum(enumType) :
                    enumType.EnumValues.FirstOrDefault()?.IntValue != null ? new RustIntegerEnum(enumType) :
                    new RustBareEnum(enumType),
                _ => throw new Exception("unrecognized schema type"),
            };

            acceptor(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
        }
    }
}
