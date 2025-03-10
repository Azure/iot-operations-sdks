namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class AvroSchemaStandardizer : ISchemaStandardizer
    {
        public SerializationFormat SerializationFormat { get => SerializationFormat.Avro; }

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaFilePath, CodeName genNamespace)
        {
            StreamReader schemaReader = File.OpenText(schemaFilePath);
            List<SchemaType> schemaTypes = new();

            using (JsonDocument schemaDoc = JsonDocument.Parse(schemaReader.ReadToEnd()))
            {
                GetSchemaType(schemaDoc.RootElement, schemaTypes, genNamespace);
            }

            return schemaTypes;
        }

        private SchemaType GetSchemaType(JsonElement schemaElt, List<SchemaType> schemaTypes, CodeName parentNamespace)
        {
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
                case "boolean":
                    return new BooleanType();
                case "double":
                    return new DoubleType();
                case "float":
                    return new FloatType();
                case "int":
                    return new IntegerType();
                case "long":
                    return new LongType();
                case "string":
                    return new StringType();
                case "bytes":
                    return new BytesType();
                default:
                    throw new Exception("unrecognized schema");
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
