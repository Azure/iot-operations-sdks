namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class RustTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(string projectName, SchemaType schemaType, SerializationFormat serFormat, string outputFolder)
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

            string generatedCode = templateTransform.TransformText();
            string outDirPath = Path.Combine(outputFolder, templateTransform.FolderPath);
            if (!Directory.Exists(outDirPath))
            {
                Directory.CreateDirectory(outDirPath);
            }

            string outFilePath = Path.Combine(outDirPath, templateTransform.FileName);
            File.WriteAllText(outFilePath, generatedCode);
            Console.WriteLine($"  generated {outFilePath}");
        }
    }
}
