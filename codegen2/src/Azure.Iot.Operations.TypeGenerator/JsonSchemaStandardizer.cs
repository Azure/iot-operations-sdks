namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;

    internal class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private readonly string[] InternalDefsKeys = new string[] { "$defs", "definitions" };

        public SerializationFormat SerializationFormat { get => SerializationFormat.Json; }

        public List<SchemaType> GetStandardizedSchemas(Dictionary<string, string> schemaTextsByName)
        {
            Dictionary<string, JsonElement> rootElementsByName = GetRootElementsFromSchemaTexts(schemaTextsByName);

            List<SchemaType> schemaTypes = new();

            foreach (KeyValuePair<string, JsonElement> namedRootElt in rootElementsByName)
            {
                CollateAliasTypes(namedRootElt.Key, namedRootElt.Value, schemaTypes, rootElementsByName);
                CollateSchemaTypes(namedRootElt.Key, namedRootElt.Value, schemaTypes, rootElementsByName);

                foreach (string internalDefsKey in InternalDefsKeys)
                {
                    if (namedRootElt.Value.TryGetProperty(internalDefsKey, out JsonElement defsElt))
                    {
                        foreach (JsonProperty defProp in defsElt.EnumerateObject())
                        {
                            CollateSchemaTypes(namedRootElt.Key, defProp.Value, schemaTypes, rootElementsByName);
                        }
                    }
                }
            }

            return schemaTypes;
        }

        private Dictionary<string, JsonElement> GetRootElementsFromSchemaTexts(Dictionary<string, string> schemaTextsByName)
        {
            Dictionary<string, JsonElement> rootElementsByName = new();

            foreach (KeyValuePair<string, string> namedSchemaText in schemaTextsByName)
            {
                using (JsonDocument schemaDoc = JsonDocument.Parse(namedSchemaText.Value))
                {
                    rootElementsByName[namedSchemaText.Key] = schemaDoc.RootElement.Clone();
                }
            }

            return rootElementsByName;
        }

        internal void CollateAliasTypes(string docName, JsonElement schemaElt, List<SchemaType> schemaTypes, Dictionary<string, JsonElement> rootElementsByName)
        {
            if (!schemaElt.TryGetProperty("title", out JsonElement titleElt) || !schemaElt.TryGetProperty("$ref", out JsonElement referencingElt))
            {
                return;
            }

            string title = titleElt.GetString()!;

            JsonElement referencedElt = GetReferencedElement(docName, referencingElt, rootElementsByName);
            string refTitle = referencedElt.GetProperty("title").GetString()!;

            string? description = schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

            schemaTypes.Add(new AliasType(new CodeName(title), description, new CodeName(refTitle)));
        }

        internal void CollateSchemaTypes(string docName, JsonElement schemaElt, List<SchemaType> schemaTypes, Dictionary<string, JsonElement> rootElementsByName)
        {
            if (!schemaElt.TryGetProperty("type", out JsonElement typeElt))
            {
                return;
            }

            string type = typeElt.GetString()!;
            if (type == "object" && schemaElt.TryGetProperty("additionalProperties", out JsonElement addlPropsElt) && addlPropsElt.ValueKind == JsonValueKind.Object)
            {
                CollateSchemaTypes(docName, addlPropsElt, schemaTypes, rootElementsByName);
                return;
            }
            else if (type == "array" && schemaElt.TryGetProperty("items", out JsonElement itemsElt) && itemsElt.ValueKind == JsonValueKind.Object)
            {
                CollateSchemaTypes(docName, itemsElt, schemaTypes, rootElementsByName);
                return;
            }

            string title = schemaElt.TryGetProperty("title", out JsonElement titleElt) ? titleElt.GetString()! : "SomeDefaultTitleShouldNotBeConstValue";
            CodeName schemaName = new CodeName((char.IsNumber(title[0]) ? "_" : "") + Regex.Replace(title, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant));

            string? description = schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

            if (schemaElt.TryGetProperty("properties", out JsonElement propertiesElt) && typeElt.GetString() == "object")
            {
                foreach (JsonProperty objProp in propertiesElt.EnumerateObject())
                {
                    CollateSchemaTypes(docName, objProp.Value, schemaTypes, rootElementsByName);
                }

                HashSet<string> indirectFields = schemaElt.TryGetProperty("x-indirect", out JsonElement indirectElt) ? indirectElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                HashSet<string> requiredFields = schemaElt.TryGetProperty("required", out JsonElement requiredElt) ? requiredElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                schemaTypes.Add(new ObjectType(
                    schemaName,
                    description,
                    propertiesElt.EnumerateObject().ToDictionary(p => new CodeName(p.Name), p => GetObjectTypeFieldInfo(docName, p.Name, p.Value, indirectFields, requiredFields, rootElementsByName))));
            }
            else if (schemaElt.TryGetProperty("enum", out JsonElement enumElt))
            {
                CodeName[] enumValues = enumElt.EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray();
                schemaTypes.Add(new EnumType(
                    schemaName,
                    description,
                    enumValues));
            }
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(string docName, string fieldName, JsonElement schemaElt, HashSet<string> indirectFields, HashSet<string> requiredFields, Dictionary<string, JsonElement> rootElementsByName)
        {
            return new ObjectType.FieldInfo(
                GetSchemaTypeFromJsonElement(docName, schemaElt, rootElementsByName),
                indirectFields.Contains(fieldName),
                requiredFields.Contains(fieldName),
                schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null,
                schemaElt.TryGetProperty("index", out JsonElement indexElt) ? indexElt.GetInt32() : null);
        }

        private SchemaType GetSchemaTypeFromJsonElement(string docName, JsonElement schemaElt, Dictionary<string, JsonElement> rootElementsByName)
        {
            if (!schemaElt.TryGetProperty("$ref", out JsonElement referencingElt))
            {
                if (schemaElt.TryGetProperty("title", out JsonElement titleElt) && schemaElt.TryGetProperty("type", out JsonElement typeElt))
                {
                    if (typeElt.GetString() == "object" && (!schemaElt.TryGetProperty("additionalProperties", out JsonElement addlPropsElt) || addlPropsElt.ValueKind == JsonValueKind.False))
                    {
                        return new ReferenceType(new CodeName(titleElt.GetString()!), isNullable: true);
                    }
                    else if (typeElt.GetString() == "string" && schemaElt.TryGetProperty("enum", out _))
                    {
                        return new ReferenceType(new CodeName(titleElt.GetString()!), isNullable: false);
                    }
                }

                return GetPrimitiveTypeFromJsonElement(docName, schemaElt, rootElementsByName);
            }

            JsonElement referencedElt = GetReferencedElement(docName, referencingElt, rootElementsByName);

            if (referencedElt.TryGetProperty("properties", out _) || referencedElt.TryGetProperty("enum", out _))
            {
                string title = referencedElt.GetProperty("title").GetString()!;
                string type = referencedElt.GetProperty("type").GetString()!;
                return new ReferenceType(new CodeName(title), isNullable: type == "object");
            }

            return GetPrimitiveTypeFromJsonElement(docName, referencedElt, rootElementsByName);
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

        private SchemaType GetPrimitiveTypeFromJsonElement(string docName, JsonElement schemaElt, Dictionary<string, JsonElement> rootElementsByName)
        {
            switch (schemaElt.GetProperty("type").GetString())
            {
                case "array":
                    return new ArrayType(GetSchemaTypeFromJsonElement(docName, schemaElt.GetProperty("items"), rootElementsByName));
                case "object":
                    JsonElement typeAndAddendaElt = schemaElt.GetProperty("additionalProperties");
                    bool nullValues = TryGetNestedNullableJsonElement(ref typeAndAddendaElt);
                    return new MapType(GetSchemaTypeFromJsonElement(docName, typeAndAddendaElt, rootElementsByName), nullValues);
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

        private JsonElement GetReferencedElement(string docName, JsonElement referencingElt, Dictionary<string, JsonElement> rootElementsByName)
        {
            string refString = referencingElt.GetString()!;
            int fragIx = refString.IndexOf('#');

            string baseRef = fragIx switch
            {
                < 0 => refString,
                > 0 => refString.Substring(0, fragIx),
                0 => docName,
            };
            string fragment = fragIx < 0 ? string.Empty : refString.Substring(fragIx + 2);

            string refName = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(docName)!, baseRef)).Replace('\\', '/');
            JsonElement rootElt = rootElementsByName[refName];

            int sepIx = fragment.IndexOf('/');
            JsonElement referencedElt = sepIx > 0 ? rootElt.GetProperty(fragment.Substring(0, sepIx)).GetProperty(fragment.Substring(sepIx + 1)) : rootElt;

            return referencedElt;
        }
    }
}
