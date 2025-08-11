namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.IO;
    using System.Linq;

    public class DotNetTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(Action<string, string, string> acceptor, string projectName, SchemaType schemaType, SerializationFormat serFormat)
        {
            ITemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new DotNetObject(projectName, objectType, serFormat),
                EnumType enumType =>
                    enumType.EnumValues.FirstOrDefault()?.StringValue != null ? new DotNetStringEnum(projectName, enumType) :
                    enumType.EnumValues.FirstOrDefault()?.IntValue != null ? new DotNetIntegerEnum(projectName, enumType) :
                    new DotNetBareEnum(projectName, enumType),
                _ => throw new Exception("unrecognized schema type"),
            };

            acceptor(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
        }
    }
}
