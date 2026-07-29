// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

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
                ArrayType arrayType => $"List<{GetType(arrayType.ElementSchema)}>{(schemaType.OrNull ? "?" : "")}",
                MapType mapType => $"Dictionary<string, {GetType(mapType.ValueSchema)}>{(schemaType.OrNull ? "?" : "")}",
                ObjectType objectType => $"{objectType.SchemaName.GetTypeName(TargetLanguage.CSharp)}{(schemaType.OrNull ? "?" : "")}",
                EnumType enumType => $"{enumType.SchemaName.GetTypeName(TargetLanguage.CSharp)}{(schemaType.OrNull ? "?" : "")}",
                BooleanType _ => $"bool{(schemaType.OrNull ? "?" : "")}",
                DoubleType _ => $"double{(schemaType.OrNull ? "?" : "")}",
                FloatType _ => $"float{(schemaType.OrNull ? "?" : "")}",
                IntegerType _ => $"int{(schemaType.OrNull ? "?" : "")}",
                LongType _ => $"long{(schemaType.OrNull ? "?" : "")}",
                ByteType _ => $"sbyte{(schemaType.OrNull ? "?" : "")}",
                ShortType _ => $"short{(schemaType.OrNull ? "?" : "")}",
                UnsignedIntegerType _ => $"uint{(schemaType.OrNull ? "?" : "")}",
                UnsignedLongType _ => $"ulong{(schemaType.OrNull ? "?" : "")}",
                UnsignedByteType _ => $"byte{(schemaType.OrNull ? "?" : "")}",
                UnsignedShortType _ => $"ushort{(schemaType.OrNull ? "?" : "")}",
                DateType _ => $"DateOnly{(schemaType.OrNull ? "?" : "")}",
                DateTimeType _ => $"DateTime{(schemaType.OrNull ? "?" : "")}",
                TimeType _ => $"TimeOnly{(schemaType.OrNull ? "?" : "")}",
                DurationType _ => $"TimeSpan{(schemaType.OrNull ? "?" : "")}",
                UuidType _ => $"Guid{(schemaType.OrNull ? "?" : "")}",
                StringType _ => $"string{(schemaType.OrNull ? "?" : "")}",
                BytesType _ => $"byte[]{(schemaType.OrNull ? "?" : "")}",
                DecimalType _ => $"DecimalString{(schemaType.OrNull ? "?" : "")}",
                ReferenceType referenceType => $"{referenceType.SchemaName.GetTypeName(TargetLanguage.CSharp)}{(schemaType.OrNull ? "?" : "")}",
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
