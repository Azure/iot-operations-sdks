namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class AvroSchemaStandardizer : ISchemaStandardizer
    {
        public SerializationFormat SerializationFormat { get => SerializationFormat.Avro; }

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaText, CodeName genNamespace, Func<string, string> retriever)
        {
            List<SchemaType> schemaTypes = new();

            using (JsonDocument schemaDoc = JsonDocument.Parse(schemaText))
            {
                GetSchemaType(schemaDoc.RootElement, schemaTypes, genNamespace);
            }

            return schemaTypes;
        }

        private SchemaType GetSchemaType(JsonElement schemaElt, List<SchemaType> schemaTypes, CodeName parentNamespace)
        {
            if (schemaElt.ValueKind == JsonValueKind.String)
            {
                return this.TryGetPrimitiveType(schemaElt.GetString(), out SchemaType schemaType) ? schemaType : new ReferenceType(new CodeName(schemaElt.GetString()!), parentNamespace);
            }

            JsonElement typeElt = schemaElt.GetProperty("type");

            if (typeElt.ValueKind == JsonValueKind.Object)
            {
                return GetSchemaType(typeElt, schemaTypes, parentNamespace);
            }

            if (schemaElt.TryGetProperty("logicalType", out JsonElement logicalTypeElt))
            {
                switch (logicalTypeElt.GetString())
                {
                    case "date":
                        return new DateType();
                    case "time-millis":
                        return new TimeType();
                    case "timestamp-millis":
                        return new DateTimeType();
                }
            }

            CodeName? schemaName = schemaElt.TryGetProperty("name", out JsonElement nameElt) ? new CodeName(nameElt.GetString()!) : null;
            CodeName genNamespace = schemaElt.TryGetProperty("namespace", out JsonElement namespaceElt) && !namespaceElt.GetString()!.Contains('.') ? new CodeName(namespaceElt.GetString()!) : parentNamespace;

            if (this.TryGetPrimitiveType(typeElt.GetString(), out SchemaType primitiveType))
            {
                return primitiveType;
            }

            switch (typeElt.GetString())
            {
                case "record":
                    schemaTypes.Add(new ObjectType(
                        schemaName!,
                        genNamespace,
                        null,
                        schemaElt.GetProperty("fields").EnumerateArray().ToDictionary(e => new CodeName(e.GetProperty("name").GetString()!), e => GetObjectTypeFieldInfo(e, schemaTypes, genNamespace))));
                    return new ReferenceType(schemaName!, genNamespace);
                case "enum":
                    schemaTypes.Add(new EnumType(
                        schemaName!,
                        genNamespace,
                        null,
                        names: schemaElt.GetProperty("symbols").EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray()));
                    return new ReferenceType(schemaName!, genNamespace, isEnum: true);
                case "map":
                    return new MapType(GetSchemaType(schemaElt.GetProperty("values"), schemaTypes, genNamespace));
                case "array":
                    return new ArrayType(GetSchemaType(schemaElt.GetProperty("items"), schemaTypes, genNamespace));
                default:
                    throw new Exception($"Unrecognized schema type: {typeElt.GetString()}");
            }
        }

        private bool TryGetPrimitiveType(string? typeName, out SchemaType schemaType)
        {
            switch (typeName)
            {
                case "boolean":
                    schemaType = new BooleanType();
                    return true;
                case "double":
                    schemaType = new DoubleType();
                    return true;
                case "float":
                    schemaType = new FloatType();
                    return true;
                case "int":
                    schemaType = new IntegerType();
                    return true;
                case "long":
                    schemaType = new LongType();
                    return true;
                case "string":
                    schemaType = new StringType();
                    return true;
                case "bytes":
                    schemaType = new BytesType();
                    return true;
                default:
                    schemaType = null!;
                    return false;
            }
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(JsonElement fieldElt, List<SchemaType> schemaTypes, CodeName parentNamespace)
        {
            JsonElement typeElt = fieldElt.GetProperty("type");
            bool isOptional = typeElt.ValueKind == JsonValueKind.Array && typeElt[0].GetString() == "null";
            return isOptional ? GetNonNullTypeFieldInfo(typeElt[1], schemaTypes, parentNamespace, isRequired: false) : GetNonNullTypeFieldInfo(fieldElt, schemaTypes, parentNamespace, isRequired: true);
        }

        private ObjectType.FieldInfo GetNonNullTypeFieldInfo(JsonElement schemaElt, List<SchemaType> schemaTypes, CodeName parentNamespace, bool isRequired)
        {
            return new ObjectType.FieldInfo(GetSchemaType(schemaElt, schemaTypes, parentNamespace), isRequired, null, null);
        }
    }
}
