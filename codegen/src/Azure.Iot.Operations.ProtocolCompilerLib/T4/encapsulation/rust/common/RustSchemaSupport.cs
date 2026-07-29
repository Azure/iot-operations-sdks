namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Text;

    public static class RustSchemaSupport
    {
        public static string GetType(SchemaType schemaType, bool isIndirect, bool isRequired)
        {
            string innerType = schemaType switch
            {
                ArrayType arrayType => $"Vec<{GetType(arrayType.ElementSchema, false, true)}>",
                MapType mapType => $"HashMap<String, {GetType(mapType.ValueSchema, false, !mapType.NullValues)}>",
                ObjectType objectType => objectType.SchemaName.GetTypeName(TargetLanguage.Rust),
                EnumType enumType => enumType.SchemaName.GetTypeName(TargetLanguage.Rust),
                BooleanType _ => "bool",
                DoubleType _ => "f64",
                FloatType _ => "f32",
                IntegerType _ => "i32",
                LongType _ => "i64",
                ByteType _ => "i8",
                ShortType _ => "i16",
                UnsignedIntegerType _ => "u32",
                UnsignedLongType _ => "u64",
                UnsignedByteType _ => "u8",
                UnsignedShortType _ => "u16",
                DateType _ => "Date",
                DateTimeType _ => "DateTime<Utc>",
                TimeType _ => "Time",
                DurationType _ => "Duration",
                UuidType _ => "Uuid",
                StringType _ => "String",
                BytesType _ => "Bytes",
                DecimalType _ => "Decimal",
                ReferenceType referenceType => referenceType.SchemaName.GetTypeName(TargetLanguage.Rust),
                _ => throw new Exception($"unrecognized SchemaType type {schemaType.GetType()}"),
            };

            string wrappedType = isIndirect ? $"Box<{innerType}>" : innerType;

            return isRequired ? wrappedType : $"Option<{wrappedType}>";
        }

        public static bool HasNativeDefault(SchemaType schemaType)
        {
            return schemaType switch
            {
                ArrayType _ => true,
                MapType _ => true,
                _ => false,
            };
        }
    }
}
