namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private const string InternalDefsKey = "$defs";
        private const string InternalRefPrefix = $"#/{InternalDefsKey}/";

        public SerializationFormat SerializationFormat { get => SerializationFormat.Json; }

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaFilePath, CodeName genNamespace)
        {
            List<SchemaType> schemaTypes = new();

            using (StreamReader schemaReader = File.OpenText(schemaFilePath))
            {
                using (JsonDocument schemaDoc = JsonDocument.Parse(schemaReader.ReadToEnd()))
                {
                    CollateSchemaTypes(schemaDoc.RootElement, schemaDoc.RootElement, schemaFilePath, genNamespace, schemaTypes);

                    if (schemaDoc.RootElement.TryGetProperty(InternalDefsKey, out JsonElement defsElt))
                    {
                        foreach (JsonProperty defProp in defsElt.EnumerateObject())
                        {
                            CollateSchemaTypes(schemaDoc.RootElement, defProp.Value, schemaFilePath, genNamespace, schemaTypes);
                        }
                    }
                }
            }

            return schemaTypes;
        }

        public void CollateSchemaTypes(JsonElement rootElt, JsonElement schemaElt, string schemaFilePath, CodeName genNamespace, List<SchemaType> schemaTypes)
        {
            CodeName schemaName = new CodeName(schemaElt.GetProperty("title").GetString()!);
            string? description = schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

            if (schemaElt.TryGetProperty("properties", out JsonElement propertiesElt) && schemaElt.GetProperty("type").GetString() == "object")
            {
                HashSet<string> requiredFields = schemaElt.TryGetProperty("required", out JsonElement requiredElt) ? requiredElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                schemaTypes.Add(new ObjectType(
                    schemaName,
                    genNamespace,
                    description,
                    propertiesElt.EnumerateObject().ToDictionary(p => new CodeName(p.Name), p => GetObjectTypeFieldInfo(rootElt, p.Name, p.Value, requiredFields, schemaFilePath, genNamespace))));
            }
            else if (schemaElt.TryGetProperty("enum", out JsonElement enumElt))
            {
                switch (schemaElt.GetProperty("type").GetString())
                {
                    case "integer":
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            genNamespace,
                            description,
                            names: schemaElt.GetProperty("x-enumNames").EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray(),
                            intValues: enumElt.EnumerateArray().Select(e => e.GetInt32()).ToArray()));
                        break;
                    case "string":
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            genNamespace,
                            description,
                            names: schemaElt.GetProperty("x-enumNames").EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray(),
                            stringValues: enumElt.EnumerateArray().Select(e => e.GetString()!).ToArray()));
                        break;
                }
            }
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(JsonElement rootElt, string fieldName, JsonElement schemaElt, HashSet<string> requiredFields, string schemaFilePath, CodeName genNamespace)
        {
            return new ObjectType.FieldInfo(
                GetSchemaTypeFromJsonElement(rootElt, schemaElt, schemaFilePath, genNamespace),
                requiredFields.Contains(fieldName),
                schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null,
                schemaElt.TryGetProperty("index", out JsonElement indexElt) ? indexElt.GetInt32() : null);
        }

        private SchemaType GetSchemaTypeFromJsonElement(JsonElement rootElt, JsonElement schemaElt, string schemaFilePath, CodeName genNamespace)
        {
            if (!schemaElt.TryGetProperty("$ref", out JsonElement referencingElt))
            {
                return GetPrimitiveTypeFromJsonElement(rootElt, schemaElt, schemaFilePath, genNamespace);
            }

            string refString = referencingElt.GetString()!;

            if (!refString.StartsWith(InternalRefPrefix))
            {
                string refFilePath = Path.Combine(Path.GetDirectoryName(schemaFilePath)!, refString);
                GetTitleTypeAndNamespace(refFilePath, out string title, out string type, out genNamespace);
                return new ReferenceType(new CodeName(title), genNamespace, isNullable: type == "object");
            }

            JsonElement referencedElt = rootElt.GetProperty(InternalDefsKey).GetProperty(refString.Substring(InternalRefPrefix.Length));

            if (referencedElt.TryGetProperty("properties", out _) || referencedElt.TryGetProperty("enum", out _))
            {
                string title = referencedElt.GetProperty("title").GetString()!;
                string type = referencedElt.GetProperty("type").GetString()!;
                return new ReferenceType(new CodeName(title), genNamespace, isNullable: type == "object");
            }

            return GetPrimitiveTypeFromJsonElement(rootElt, referencedElt, schemaFilePath, genNamespace);
        }

        private SchemaType GetPrimitiveTypeFromJsonElement(JsonElement rootElt, JsonElement schemaElt, string schemaFilePath, CodeName genNamespace)
        {
            switch (schemaElt.GetProperty("type").GetString())
            {
                case "array":
                    return new ArrayType(GetSchemaTypeFromJsonElement(rootElt, schemaElt.GetProperty("items"), schemaFilePath, genNamespace));
                case "object":
                    return new MapType(GetSchemaTypeFromJsonElement(rootElt, schemaElt.GetProperty("additionalProperties"), schemaFilePath, genNamespace));
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

        private void GetTitleTypeAndNamespace(string refFilePath, out string title, out string type, out CodeName genNamespace)
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
                    genNamespace = new CodeName(Path.GetFileName(Path.GetDirectoryName(refFilePath))!);
                }
            }
        }
    }
}
