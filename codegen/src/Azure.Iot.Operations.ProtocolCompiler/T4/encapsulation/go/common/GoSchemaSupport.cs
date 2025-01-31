
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public static class GoSchemaSupport
    {
        public static string GetType(SchemaType schemaType, bool isRequired)
        {
            string optRef = isRequired ? string.Empty : "*";
            return schemaType switch
            {
                ArrayType arrayType => $"[]{GetType(arrayType.ElementSchema, true)}",
                MapType mapType => $"map[string]{GetType(mapType.ValueSchema, true)}",
                ObjectType objectType => $"{optRef}{objectType.SchemaName.GetTypeName(TargetLanguage.Go)}",
                EnumType enumType => $"{optRef}{enumType.SchemaName.GetTypeName(TargetLanguage.Go)}",
                BooleanType _ => $"{optRef}bool",
                DoubleType _ => $"{optRef}float64",
                FloatType _ => $"{optRef}float32",
                IntegerType _ => $"{optRef}int32",
                LongType _ => $"{optRef}int64",
                ByteType _ => $"{optRef}int8",
                ShortType _ => $"{optRef}int16",
                UnsignedIntegerType _ => $"{optRef}uint32",
                UnsignedLongType _ => $"{optRef}uint64",
                UnsignedByteType _ => $"{optRef}uint8",
                UnsignedShortType _ => $"{optRef}uint16",
                DateType _ => $"{optRef}iso.Time",
                DateTimeType _ => $"{optRef}iso.Time",
                TimeType _ => $"{optRef}iso.Time",
                DurationType _ => $"{optRef}iso.Duration",
                UuidType _ => $"{optRef}uuid.UUID",
                StringType _ => $"{optRef}string",
                BytesType _ => $"{optRef}iso.ByteSlice",
                DecimalType _ => $"{optRef}decimal.Decimal",
                ReferenceType referenceType => $"{optRef}{referenceType.SchemaName.GetTypeName(TargetLanguage.Go)}",
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
                case BytesType:
                    schemaImport = "github.com/Azure/iot-operations-sdks/go/protocol/iso";
                    return true;
                case UuidType:
                    schemaImport = "github.com/google/uuid";
                    return true;
                case DecimalType:
                    schemaImport = "github.com/shopspring/decimal";
                    return true;
                default:
                    schemaImport = string.Empty;
                    return false;
            }
        }
    }
}
