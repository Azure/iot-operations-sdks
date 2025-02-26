namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.IO;
    using System.Linq;

    public class DotNetTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(string projectName, CodeName genNamespace, SchemaType schemaType, SerializationFormat serFormat, string outputFolder)
        {
            ITemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new DotNetObject(projectName, genNamespace, objectType, serFormat),
                EnumType enumType =>
                    enumType.EnumValues.FirstOrDefault()?.StringValue != null ? new DotNetStringEnum(projectName, genNamespace, enumType) :
                    enumType.EnumValues.FirstOrDefault()?.IntValue != null ? new DotNetIntegerEnum(projectName, genNamespace, enumType) :
                    new DotNetBareEnum(projectName, genNamespace, enumType),
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
