namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private const string JsonSchemaFileSuffix = ".schema.json";

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaFilePath)
        {
            StreamReader schemaReader = File.OpenText(schemaFilePath);
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
                            schemaDoc.RootElement.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name, p => GetObjectTypeFieldInfo(p.Name, p.Value, requiredFields, schemaFilePath))));
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

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(string fieldName, JsonElement schemaElt, HashSet<string> requiredFields, string schemaFilePath)
        {
            return new ObjectType.FieldInfo(
                GetSchemaTypeFromJsonElement(schemaElt, schemaFilePath),
                requiredFields.Contains(fieldName),
                schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null,
                schemaElt.TryGetProperty("index", out JsonElement indexElt) ? indexElt.GetInt32() : null);
        }

        private SchemaType GetSchemaTypeFromJsonElement(JsonElement schemaElt, string schemaFilePath)
        {
            if (schemaElt.TryGetProperty("$ref", out JsonElement refElt))
            {
                string refFilePath = Path.Combine(Path.GetDirectoryName(schemaFilePath)!, refElt.GetString()!);
                return new ReferenceType(refElt.GetString()!.Replace(JsonSchemaFileSuffix, string.Empty), IsNullable(refFilePath));
            }
            switch (schemaElt.GetProperty("type").GetString())
            {
                case "array":
                    return new ArrayType(GetSchemaTypeFromJsonElement(schemaElt.GetProperty("items"), schemaFilePath));
                case "object":
                    return new MapType(GetSchemaTypeFromJsonElement(schemaElt.GetProperty("additionalProperties"), schemaFilePath));
                case "boolean":
                    return new BooleanType();
                case "number":
                    return schemaElt.GetProperty("format").GetString() == "float" ? new FloatType() : new DoubleType();
                case "integer":
                    return schemaElt.GetProperty("maximum").GetUInt64() switch
                    {
                        < 128 => new ByteType(),
                        < 256 => new UnsignedByteType(),
                        < 32768 => new ShortType(),
                        < 65536 => new UnsignedShortType(),
                        < 2147483648 => new IntegerType(),
                        < 4294967296 => new UnsignedIntegerType(),
                        < 9223372036854775808 => new LongType(),
                        _ => new UnsignedLongType(),
                    };
                case "string":
                    if (schemaElt.TryGetProperty("format", out JsonElement formatElt))
                    {
                        return formatElt.GetString() switch
                        {
                            "date" => new DateType(),
                            "date-time" => new DateTimeType(),
                            "time" => new TimeType(),
                            "duration" => new DurationType(),
                            "uuid" => new UuidType(),
                            _ => throw new Exception($"unrecognized 'string' schema (format = {formatElt.GetString()})"),
                        };
                    }

                    if (schemaElt.TryGetProperty("contentEncoding", out JsonElement encodingElt))
                    {
                        return encodingElt.GetString() switch
                        {
                            "base64" => new BytesType(),
                            _ => throw new Exception($"unrecognized 'string' schema (contentEncoding = {encodingElt.GetString()})"),
                        };
                    }

                    if (schemaElt.TryGetProperty("pattern", out JsonElement patternElt))
                    {
                        return patternElt.GetString() switch
                        {
                            "^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$" => new DecimalType(),
                            _ => throw new Exception($"unrecognized 'string' schema (pattern = {patternElt.GetString()})"),
                        };
                    }

                    return new StringType();
                default:
                    throw new Exception($"unrecognized schema (type = {schemaElt.GetProperty("type").GetString()})");
            }
        }

        private bool IsNullable(string refFilePath)
        {
            StreamReader refReader = File.OpenText(refFilePath);
            using (JsonDocument refDoc = JsonDocument.Parse(refReader.ReadToEnd()))
            {
                return refDoc.RootElement.GetProperty("type").GetString() == "object";
            }
        }
    }
}
