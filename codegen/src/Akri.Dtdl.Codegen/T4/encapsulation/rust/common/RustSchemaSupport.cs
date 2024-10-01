namespace Akri.Dtdl.Codegen
{
    using System.Text;

    public static class RustSchemaSupport
    {
        public static string GetType(SchemaType schemaType, bool isRequired)
        {
            string innerType = schemaType switch
            {
                ArrayType arrayType => $"Vec<{GetType(arrayType.ElementSchmema, true)}>",
                MapType mapType => $"HashMap<String, {GetType(mapType.ValueSchema, true)}>",
                ObjectType objectType => objectType.SchemaName,
                EnumType enumType => enumType.SchemaName,
                BooleanType _ => "bool",
                DoubleType _ => "f64",
                FloatType _ => "f32",
                IntegerType _ => "i32",
                LongType _ => "i64",
                ByteType => "i8",
                ShortType => "i16",
                UnsignedIntegerType _ => "u32",
                UnsignedLongType _ => "u64",
                UnsignedByteType => "u8",
                UnsignedShortType => "u16",
                DateType _ => "Date",
                DateTimeType _ => "DateTime<Utc>",
                TimeType _ => "Time",
                DurationType _ => "Duration",
                UuidType _ => "placeholder for proper Rust uuid type",
                StringType _ => "String",
                BytesType _ => "placeholder for proper Rust bytes type",
                ReferenceType referenceType => referenceType.SchemaName,
                _ => throw new Exception($"unrecognized SchemaType type {schemaType.GetType()}"),
            };

            return isRequired ? innerType : $"Option<{innerType}>";
        }
    }
}
