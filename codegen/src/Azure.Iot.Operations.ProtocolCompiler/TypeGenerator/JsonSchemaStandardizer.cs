namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class JsonSchemaStandardizer : ISchemaStandardizer
    {
        public SerializationFormat SerializationFormat { get => SerializationFormat.Json; }

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaFilePath)
        {
            StreamReader schemaReader = File.OpenText(schemaFilePath);
            List<SchemaType> schemaTypes = new();

            using (JsonDocument schemaDoc = JsonDocument.Parse(schemaReader.ReadToEnd()))
            {
                CodeName schemaName = new CodeName(schemaDoc.RootElement.GetProperty("title").GetString()!);
                string? description = schemaDoc.RootElement.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

                switch (schemaDoc.RootElement.GetProperty("type").GetString())
                {
                    case "object":
                        HashSet<string> requiredFields = schemaDoc.RootElement.TryGetProperty("required", out JsonElement requredElt) ? requredElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                        schemaTypes.Add(new ObjectType(
                            schemaName,
                            description,
                            schemaDoc.RootElement.GetProperty("properties").EnumerateObject().ToDictionary(p => new CodeName(p.Name), p => GetObjectTypeFieldInfo(p.Name, p.Value, requiredFields, schemaFilePath))));
                        break;
                    case "integer":
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            description,
                            names: schemaDoc.RootElement.GetProperty("x-enumNames").EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray(),
                            intValues: schemaDoc.RootElement.GetProperty("enum").EnumerateArray().Select(e => e.GetInt32()).ToArray()));
                        break;
                    case "string":
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            description,
                            names: schemaDoc.RootElement.GetProperty("x-enumNames").EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray(),
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
                GetTitleAndType(refFilePath, out string title, out string type);
                return new ReferenceType(new CodeName(title), isNullable: type == "object");
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

        private void GetTitleAndType(string refFilePath, out string title, out string type)
        {
            StreamReader refReader = File.OpenText(refFilePath);
            using (JsonDocument refDoc = JsonDocument.Parse(refReader.ReadToEnd()))
            {
                JsonElement rootElt = refDoc.RootElement;
                if (!rootElt.TryGetProperty("title", out JsonElement titleElt))
                {
                    throw new Exception($"JSON schema {refFilePath} missing title");
                }
                else if (!rootElt.TryGetProperty("type", out JsonElement typeElt))
                {
                    throw new Exception($"JSON schema {refFilePath} missing type");
                }
                else
                {
                    title = titleElt.GetString()!;
                    type = typeElt.GetString()!;
                }
            }
        }
    }
}
