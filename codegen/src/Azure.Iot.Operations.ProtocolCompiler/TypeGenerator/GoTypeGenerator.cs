namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class GoTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(string projectName, string genNamespace, SchemaType schemaType, string outputFolder, HashSet<string> sourceFilePaths)
        {
            ITemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new GoObject(genNamespace, objectType, GetSchemaImports(objectType)),
                EnumType enumType => enumType.EnumValues.FirstOrDefault()?.StringValue != null ? new GoStringEnum(genNamespace, enumType) : new GoIntegerEnum(genNamespace, enumType),
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
            sourceFilePaths.Add(outFilePath);
        }


        private IReadOnlyCollection<string> GetSchemaImports(SchemaType schemaType)
        {
            HashSet<string> schemaImports = new();
            AddSchemaImports(schemaImports, schemaType);
            return schemaImports;
        }

        private void AddSchemaImports(HashSet<string> schemaImports, SchemaType schemaType)
        {
            switch (schemaType)
            {
                case ArrayType arrayType:
                    AddSchemaImports(schemaImports, arrayType.ElementSchema);
                    break;
                case MapType mapType:
                    AddSchemaImports(schemaImports, mapType.ValueSchema);
                    break;
                case ObjectType objectType:
                    foreach (var fieldInfo in objectType.FieldInfos)
                    {
                        AddSchemaImports(schemaImports, fieldInfo.Value.SchemaType);
                    }
                    break;
                default:
                    if (GoSchemaSupport.TryGetImport(schemaType, out string schemaImport))
                    {
                        schemaImports.Add(schemaImport);
                    }
                    break;
            }
        }
    }
}
