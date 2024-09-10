
namespace Akri.Dtdl.Codegen
{
    public static class DotNetSchemaSupport
    {
        public static string GetType(SchemaType schemaType)
        {
            return schemaType switch
            {
                ArrayType arrayType => $"List<{GetType(arrayType.ElementSchmema)}>",
                MapType mapType => $"Dictionary<string, {GetType(mapType.ValueSchema)}>",
                ObjectType objectType => objectType.SchemaName,
                EnumType enumType => enumType.SchemaName,
                BooleanType _ => "bool",
                DoubleType _ => "double",
                FloatType _ => "float",
                IntegerType _ => "int",
                LongType _ => "long",
                DateType _ => "DateOnly",
                DateTimeType _ => "DateTime",
                TimeType _ => "TimeOnly",
                DurationType _ => "TimeSpan",
                UuidType => "Guid",
                StringType _ => "string",
                ReferenceType referenceType => referenceType.SchemaName,
                _ => throw new Exception($"unrecognized SchemaType type {schemaType.GetType()}"),
            };
        }
    }
}
