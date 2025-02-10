namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class RustTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(string projectName, CodeName genNamespace, SchemaType schemaType, SerializationFormat serFormat, string outputFolder, HashSet<string> sourceFilePaths)
        {
            ITemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new RustObject(genNamespace, objectType, GetReferencedSchemaNames(objectType)),
                EnumType enumType =>
                    enumType.EnumValues.FirstOrDefault()?.StringValue != null ? new RustStringEnum(genNamespace, enumType) :
                    enumType.EnumValues.FirstOrDefault()?.IntValue != null ? new RustIntegerEnum(genNamespace, enumType) :
                    new RustBareEnum(genNamespace, enumType),
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

        private IReadOnlyCollection<CodeName> GetReferencedSchemaNames(SchemaType schemaType)
        {
            HashSet<CodeName> referencedSchemaNames = new();
            AddReferencedSchemaNames(referencedSchemaNames, schemaType);
            return referencedSchemaNames;
        }

        private void AddReferencedSchemaNames(HashSet<CodeName> referencedSchemaNames, SchemaType schemaType)
        {
            switch (schemaType)
            {
                case ArrayType arrayType:
                    AddReferencedSchemaNames(referencedSchemaNames, arrayType.ElementSchema);
                    break;
                case MapType mapType:
                    AddReferencedSchemaNames(referencedSchemaNames, mapType.ValueSchema);
                    break;
                case ObjectType objectType:
                    foreach (var fieldInfo in objectType.FieldInfos)
                    {
                        AddReferencedSchemaNames(referencedSchemaNames, fieldInfo.Value.SchemaType);
                    }
                    break;
                case ReferenceType referenceType:
                    referencedSchemaNames.Add(referenceType.SchemaName);
                    break;
            }
        }
    }
}
