namespace Akri.Dtdl.Codegen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private const string JsonSchemaFileSuffix = ".schema.json";

        public IEnumerable<SchemaType> GetStandardizedSchemas(StreamReader schemaReader)
        {
            List<SchemaType> schemaTypes = new();

            using (JsonDocument schemaDoc = JsonDocument.Parse(schemaReader.ReadToEnd()))
            {
                string schemaName = schemaDoc.RootElement.GetProperty("title").GetString()!;
                string? description = schemaDoc.RootElement.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

                switch (schemaDoc.RootElement.GetProperty("type").GetString())
                {
                    case "object":
                        HashSet<string> requiredFields = schemaDoc.RootElement.TryGetProperty("required", out JsonElement requredElt) ? requredElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                        schemaTypes.Add(new ObjectType(
                            schemaName,
                            description,
                            schemaDoc.RootElement.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name, p => GetObjectTypeFieldInfo(p.Name, p.Value, requiredFields))));
                        break;
                    case "integer":
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            description,
                            names: schemaDoc.RootElement.GetProperty("x-enumNames").EnumerateArray().Select(e => e.GetString()!).ToArray(),
                            intValues: schemaDoc.RootElement.GetProperty("enum").EnumerateArray().Select(e => e.GetInt32()).ToArray()));
                        break;
                    case "string":
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            description,
                            names: schemaDoc.RootElement.GetProperty("x-enumNames").EnumerateArray().Select(e => e.GetString()!).ToArray(),
                            stringValues: schemaDoc.RootElement.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToArray()));
                        break;
                }
            }

            return schemaTypes;
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(string fieldName, JsonElement schemaElt, HashSet<string> requiredFields)
        {
            return new ObjectType.FieldInfo(
                GetSchemaTypeFromJsonElement(schemaElt),
                requiredFields.Contains(fieldName),
                schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null,
                schemaElt.TryGetProperty("index", out JsonElement indexElt) ? indexElt.GetInt32() : null);
        }

        private SchemaType GetSchemaTypeFromJsonElement(JsonElement schemaElt)
        {
            if (schemaElt.TryGetProperty("$ref", out JsonElement refElt))
            {
                return new ReferenceType(refElt.GetString()!.Replace(JsonSchemaFileSuffix, string.Empty));
            }
            switch (schemaElt.GetProperty("type").GetString())
            {
                case "array":
                    return new ArrayType(GetSchemaTypeFromJsonElement(schemaElt.GetProperty("items")));
                case "object":
                    return new MapType(GetSchemaTypeFromJsonElement(schemaElt.GetProperty("additionalProperties")));
                case "boolean":
                    return new BooleanType();
                default:
                    if (schemaElt.TryGetProperty("format", out JsonElement formatElt))
                    {
                        return formatElt.GetString() switch
                        {
                            "double" => new DoubleType(),
                            "float" => new FloatType(),
                            "int32" => new IntegerType(),
                            "int64" => new LongType(),
                            "date" => new DateType(),
                            "date-time" => new DateTimeType(),
                            "time" => new TimeType(),
                            "duration" => new DurationType(),
                            "uuid" => new UuidType(),
                            _ => throw new Exception($"unrecognized schema (format = {formatElt.GetString()})"),
                        };
                    }

                    if (schemaElt.TryGetProperty("contentEncoding", out JsonElement encodingElt))
                    {
                        return encodingElt.GetString() switch
                        {
                            "base64" => new BytesType(),
                            _ => throw new Exception($"unrecognized schema (contentEncoding = {encodingElt.GetString()})"),
                        };
                    }

                    return new StringType();
            }
        }
    }
}
