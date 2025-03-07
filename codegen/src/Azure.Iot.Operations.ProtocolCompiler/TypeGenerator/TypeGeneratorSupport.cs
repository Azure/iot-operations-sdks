namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public static class TypeGeneratorSupport
    {
        public static IReadOnlyCollection<ReferenceType> GetReferencedSchemas(SchemaType schemaType)
        {
            HashSet<ReferenceType> referencedSchemas = new();
            AddReferencedSchemaNames(referencedSchemas, schemaType);
            return referencedSchemas;
        }

        private static void AddReferencedSchemaNames(HashSet<ReferenceType> referencedSchemas, SchemaType schemaType)
        {
            switch (schemaType)
            {
                case ArrayType arrayType:
                    AddReferencedSchemaNames(referencedSchemas, arrayType.ElementSchema);
                    break;
                case MapType mapType:
                    AddReferencedSchemaNames(referencedSchemas, mapType.ValueSchema);
                    break;
                case ObjectType objectType:
                    foreach (var fieldInfo in objectType.FieldInfos)
                    {
                        AddReferencedSchemaNames(referencedSchemas, fieldInfo.Value.SchemaType);
                    }
                    break;
                case ReferenceType referenceType:
                    referencedSchemas.Add(referenceType);
                    break;
            }
        }
    }
}
