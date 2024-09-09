
namespace Akri.Dtdl.Codegen
{
    public static class GoSchemaSupport
    {
        public static string GetType(SchemaType schemaType, bool isRequired)
        {
            string optRef = isRequired ? string.Empty : "*";
            return schemaType switch
            {
                ArrayType arrayType => $"[]{GetType(arrayType.ElementSchmema, true)}",
                MapType mapType => $"map[string]{GetType(mapType.ValueSchema, true)}",
                ObjectType objectType => $"{optRef}{objectType.SchemaName}",
                EnumType enumType => $"{optRef}{enumType.SchemaName}",
                BooleanType _ => $"{optRef}bool",
                DoubleType _ => $"{optRef}float64",
                FloatType _ => $"{optRef}float32",
                IntegerType _ => $"{optRef}int32",
                LongType _ => $"{optRef}int64",
                DateType _ => $"{optRef}iso.Time",
                DateTimeType _ => $"{optRef}iso.Time",
                TimeType _ => $"{optRef}iso.Time",
                DurationType _ => $"{optRef}iso.Duration",
                UuidType => "placeholder for proper Go uuid type",
                StringType _ => $"{optRef}string",
                ReferenceType referenceType => $"{optRef}{referenceType.SchemaName}",
                _ => throw new Exception($"unrecognized SchemaType type {schemaType.GetType()}"),
            };
        }

        public static bool TryGetImport(SchemaType schemaType, out string schemaImport)
        {
            switch (schemaType)
            {
                case DateType:
                case TimeType:
                case DateTimeType:
                case DurationType:
                    schemaImport = "github.com/Azure/iot-operations-sdks/go/protocol/iso";
                    return true;
                default:
                    schemaImport = string.Empty;
                    return false;
            }
        }
    }
}
