namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private readonly string[] InternalDefsKeys = new string[] { "$defs", "definitions" };

        public SerializationFormat SerializationFormat { get => SerializationFormat.Json; }

        public IEnumerable<SchemaType> GetStandardizedSchemas(string schemaText, CodeName genNamespace, Func<string, string> retriever)
        {
            List<SchemaType> schemaTypes = new();

            using (JsonDocument schemaDoc = JsonDocument.Parse(schemaText))
            {
                string? internalDefsKey = null;
                foreach (string key in InternalDefsKeys)
                {
                    if (schemaDoc.RootElement.TryGetProperty(key, out _))
                    {
                        internalDefsKey = key;
                        break;
                    }
                }

                CollateSchemaTypes(schemaDoc.RootElement, schemaDoc.RootElement, internalDefsKey, genNamespace, schemaTypes, retriever);

                if (internalDefsKey != null)
                {
                    if (schemaDoc.RootElement.TryGetProperty(internalDefsKey, out JsonElement defsElt))
                    {
                        foreach (JsonProperty defProp in defsElt.EnumerateObject())
                        {
                            CollateSchemaTypes(schemaDoc.RootElement, defProp.Value, internalDefsKey, genNamespace, schemaTypes, retriever);
                        }
                    }
                }
            }

            return schemaTypes;
        }

        public void CollateSchemaTypes(JsonElement rootElt, JsonElement schemaElt, string? internalDefsKey, CodeName genNamespace, List<SchemaType> schemaTypes, Func<string, string> retriever)
        {
            string? title = schemaElt.GetProperty("title").GetString();
            if (string.IsNullOrEmpty(title))
            {
                throw new InvalidOperationException($"The 'title' property is missing or empty in the JSON schema.");
            }
            CodeName schemaName = new CodeName((char.IsNumber(title[0]) ? "_" : "") + Regex.Replace(title, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant));

            string? description = schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

            if (schemaElt.TryGetProperty("properties", out JsonElement propertiesElt) && schemaElt.GetProperty("type").GetString() == "object")
            {
                HashSet<string> indirectFields = schemaElt.TryGetProperty("x-indirect", out JsonElement indirectElt) ? indirectElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                HashSet<string> requiredFields = schemaElt.TryGetProperty("required", out JsonElement requiredElt) ? requiredElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                schemaTypes.Add(new ObjectType(
                    schemaName,
                    genNamespace,
                    description,
                    propertiesElt.EnumerateObject().ToDictionary(p => new CodeName(p.Name), p => GetObjectTypeFieldInfo(rootElt, p.Name, p.Value, internalDefsKey, indirectFields, requiredFields, genNamespace, retriever))));
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
                        string[] stringValues = enumElt.EnumerateArray().Select(e => e.GetString()!).ToArray();
                        CodeName[] enumNames = schemaElt.TryGetProperty("x-enumNames", out JsonElement enumNameElt) ?
                            enumNameElt.EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray() :
                            stringValues.Select(v => new CodeName(v)).ToArray();
                        schemaTypes.Add(new EnumType(
                            schemaName,
                            genNamespace,
                            description,
                            names: enumNames,
                            stringValues: stringValues));
                        break;
                }
            }
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(JsonElement rootElt, string fieldName, JsonElement schemaElt, string? internalDefsKey, HashSet<string> indirectFields, HashSet<string> requiredFields, CodeName genNamespace, Func<string, string> retriever)
        {
            return new ObjectType.FieldInfo(
                GetSchemaTypeFromJsonElement(rootElt, schemaElt, internalDefsKey, genNamespace, retriever),
                indirectFields.Contains(fieldName),
                requiredFields.Contains(fieldName),
                schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null,
                schemaElt.TryGetProperty("index", out JsonElement indexElt) ? indexElt.GetInt32() : null);
        }

        private SchemaType GetSchemaTypeFromJsonElement(JsonElement rootElt, JsonElement schemaElt, string? internalDefsKey, CodeName genNamespace, Func<string, string> retriever)
        {
            if (!schemaElt.TryGetProperty("$ref", out JsonElement referencingElt))
            {
                return GetPrimitiveTypeFromJsonElement(rootElt, schemaElt, internalDefsKey, genNamespace, retriever);
            }

            string refString = referencingElt.GetString()!;

            if (internalDefsKey == null || !refString.StartsWith($"#/{internalDefsKey}/"))
            {
                using (JsonDocument refDoc = JsonDocument.Parse(retriever(refString)))
                {
                    string title = refDoc.RootElement.GetProperty("title").GetString()!;
                    string type = refDoc.RootElement.GetProperty("type").GetString()!;
                    return new ReferenceType(new CodeName(title), genNamespace, isNullable: type == "object");
                }
            }

            JsonElement referencedElt = rootElt.GetProperty(internalDefsKey).GetProperty(refString.Substring($"#/{internalDefsKey}/".Length));

            if (referencedElt.TryGetProperty("properties", out _) || referencedElt.TryGetProperty("enum", out _))
            {
                string title = referencedElt.GetProperty("title").GetString()!;
                string type = referencedElt.GetProperty("type").GetString()!;
                return new ReferenceType(new CodeName(title), genNamespace, isNullable: type == "object");
            }

            return GetPrimitiveTypeFromJsonElement(rootElt, referencedElt, internalDefsKey, genNamespace, retriever);
        }

        private bool TryGetNestedNullableJsonElement(ref JsonElement jsonElement)
        {
            if (jsonElement.TryGetProperty("anyOf", out JsonElement anyOfElt) && anyOfElt.ValueKind == JsonValueKind.Array)
            {
                if (anyOfElt[0].TryGetProperty("type", out JsonElement typeElt) && typeElt.GetString() == "null")
                {
                    jsonElement = anyOfElt[1];
                    return true;
                }
                else if (anyOfElt[1].TryGetProperty("type", out typeElt) && typeElt.GetString() == "null")
                {
                    jsonElement = anyOfElt[0];
                    return true;
                }
            }

            return false;
        }

        private SchemaType GetPrimitiveTypeFromJsonElement(JsonElement rootElt, JsonElement schemaElt, string? internalDefsKey, CodeName genNamespace, Func<string, string> retriever)
        {
            switch (schemaElt.GetProperty("type").GetString())
            {
                case "array":
                    return new ArrayType(GetSchemaTypeFromJsonElement(rootElt, schemaElt.GetProperty("items"), internalDefsKey, genNamespace, retriever));
                case "object":
                    JsonElement typeAndAddendaElt = schemaElt.GetProperty("additionalProperties");
                    bool nullValues = TryGetNestedNullableJsonElement(ref typeAndAddendaElt);
                    return new MapType(GetSchemaTypeFromJsonElement(rootElt, typeAndAddendaElt, internalDefsKey, genNamespace, retriever), nullValues);
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
    }
}
