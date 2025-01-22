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

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaFilePath)
        {
            StreamReader schemaReader = File.OpenText(schemaFilePath);
            List<SchemaType> schemaTypes = new();

            using (JsonDocument schemaDoc = JsonDocument.Parse(schemaReader.ReadToEnd()))
            {
                GetSchemaType(schemaDoc.RootElement, schemaTypes);
            }

            return schemaTypes;
        }

        private SchemaType GetSchemaType(JsonElement schemaElt, List<SchemaType> schemaTypes)
        {
            JsonElement typeElt = schemaElt.GetProperty("type");

            if (typeElt.ValueKind == JsonValueKind.Object)
            {
                return GetSchemaType(typeElt, schemaTypes);
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

            string schemaName = schemaElt.TryGetProperty("name", out JsonElement nameElt) ? nameElt.GetString() ?? string.Empty : string.Empty;

            switch (typeElt.GetString())
            {
                case "record":
                    schemaTypes.Add(new ObjectType(
                        schemaName,
                        null,
                        schemaElt.GetProperty("fields").EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!, e => GetObjectTypeFieldInfo(e, schemaTypes))));
                    return new ReferenceType(schemaName);
                case "enum":
                    schemaTypes.Add(new EnumType(
                        schemaName,
                        null,
                        names: schemaElt.GetProperty("symbols").EnumerateArray().Select(e => e.GetString()!).ToArray()));
                    return new ReferenceType(schemaName, isEnum: true);
                case "map":
                    return new MapType(GetSchemaType(schemaElt.GetProperty("values"), schemaTypes));
                case "array":
                    return new ArrayType(GetSchemaType(schemaElt.GetProperty("items"), schemaTypes));
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
                default:
                    throw new Exception("unrecognized schema");
            }
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(JsonElement fieldElt, List<SchemaType> schemaTypes)
        {
            JsonElement typeElt = fieldElt.GetProperty("type");
            bool isOptional = typeElt.ValueKind == JsonValueKind.Array && typeElt[0].GetString() == "null";
            return isOptional ? GetNonNullTypeFieldInfo(typeElt[1], schemaTypes, isRequired: false) : GetNonNullTypeFieldInfo(fieldElt, schemaTypes, isRequired: true);
        }

        private ObjectType.FieldInfo GetNonNullTypeFieldInfo(JsonElement schemaElt, List<SchemaType> schemaTypes, bool isRequired)
        {
            return new ObjectType.FieldInfo(GetSchemaType(schemaElt, schemaTypes), isRequired, null, null);
        }
    }
}
