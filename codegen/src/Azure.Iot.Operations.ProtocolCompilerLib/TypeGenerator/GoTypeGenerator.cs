namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class GoTypeGenerator : ITypeGenerator
    {
        public void GenerateTypeFromSchema(Action<string, string, string> acceptor, string projectName, SchemaType schemaType, SerializationFormat serFormat)
        {
            ITemplateTransform templateTransform = schemaType switch
            {
                ObjectType objectType => new GoObject(objectType, GetSchemaImports(objectType)),
                EnumType enumType => enumType.EnumValues.FirstOrDefault()?.StringValue != null ? new GoStringEnum(enumType) : new GoIntegerEnum(enumType),
                _ => throw new Exception("unrecognized schema type"),
            };

            acceptor(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
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
