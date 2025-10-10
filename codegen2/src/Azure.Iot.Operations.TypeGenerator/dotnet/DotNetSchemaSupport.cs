namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using Azure.Iot.Operations.CodeGeneration;

    internal static class DotNetSchemaSupport
    {
        internal static string GetType(SchemaType schemaType)
        {
            return schemaType switch
            {
                ArrayType arrayType => $"List<{GetType(arrayType.ElementSchema)}>",
                MapType mapType => $"Dictionary<string, {GetType(mapType.ValueSchema)}{(mapType.NullValues ? "?" : "")}>",
                ObjectType objectType => objectType.SchemaName.GetTypeName(TargetLanguage.CSharp),
                EnumType enumType => enumType.SchemaName.GetTypeName(TargetLanguage.CSharp),
                BooleanType _ => "bool",
                DoubleType _ => "double",
                FloatType _ => "float",
                IntegerType _ => "int",
                LongType _ => "long",
                ByteType _ => "sbyte",
                ShortType _ => "short",
                UnsignedIntegerType _ => "uint",
                UnsignedLongType _ => "ulong",
                UnsignedByteType _ => "byte",
                UnsignedShortType _ => "ushort",
                DateType _ => "DateOnly",
                DateTimeType _ => "DateTime",
                TimeType _ => "TimeOnly",
                DurationType _ => "TimeSpan",
                UuidType _ => "Guid",
                StringType _ => "string",
                BytesType _ => "byte[]",
                DecimalType _ => "DecimalString",
                ReferenceType referenceType => referenceType.SchemaName.GetTypeName(TargetLanguage.CSharp),
                _ => throw new Exception($"unrecognized SchemaType type {schemaType.GetType()}"),
            };
        }

        internal static bool IsNullable(SchemaType schemaType)
        {
            return schemaType switch
            {
                ArrayType _ => true,
                MapType _ => true,
                ObjectType _ => true,
                StringType _ => true,
                BytesType _ => true,
                DecimalType _ => true,
                ReferenceType refType => refType.IsNullable,
                _ => false,
            };
        }
    }
}
