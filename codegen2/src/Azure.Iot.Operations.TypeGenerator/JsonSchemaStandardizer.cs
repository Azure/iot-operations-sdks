namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.CodeGeneration;

    internal class JsonSchemaStandardizer : ISchemaStandardizer
    {
        private static readonly string[] InternalDefsKeys = new string[] { "$defs", "definitions" };

        private readonly TypeNamer typeNamer;

        internal JsonSchemaStandardizer(TypeNamer typeNamer)
        {
            this.typeNamer = typeNamer;
        }

        public SerializationFormat SerializationFormat { get => SerializationFormat.Json; }

        public List<SchemaType> GetStandardizedSchemas(Dictionary<string, string> schemaTextsByName)
        {
            Dictionary<string, JsonElement> rootElementsByName = GetRootElementsFromSchemaTexts(schemaTextsByName);

            List<SchemaType> schemaTypes = new();

            foreach (KeyValuePair<string, JsonElement> namedRootElt in rootElementsByName)
            {
                CollateAliasTypes(namedRootElt.Key, namedRootElt.Value, schemaTypes, rootElementsByName);
                CollateSchemaTypes(namedRootElt.Key, null, namedRootElt.Value, schemaTypes, rootElementsByName);

                foreach (string internalDefsKey in InternalDefsKeys)
                {
                    if (namedRootElt.Value.TryGetProperty(internalDefsKey, out JsonElement defsElt))
                    {
                        foreach (JsonProperty defProp in defsElt.EnumerateObject())
                        {
                            CollateSchemaTypes(namedRootElt.Key, defProp.Name, defProp.Value, schemaTypes, rootElementsByName);
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

            CodeName schemaName = new CodeName(this.typeNamer.GenerateTypeName(docName, null, titleElt.GetString()));

            string? description = schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

            GetReferenceInfo(docName, referencingElt, rootElementsByName, out string refName, out string? refKey, out JsonElement refElt, out string? refTitle);
            CodeName referencedName = new CodeName(this.typeNamer.GenerateTypeName(refName, refKey, refTitle));

            if (!referencedName.Equals(schemaName))
            {
                schemaTypes.Add(new AliasType(schemaName, description, referencedName, orNull: false));
            }
        }

        internal void CollateSchemaTypes(string docName, string? defKey, JsonElement schemaElt, List<SchemaType> schemaTypes, Dictionary<string, JsonElement> rootElementsByName)
        {
            if (!schemaElt.TryGetProperty("type", out JsonElement typeElt))
            {
                return;
            }

            string type = typeElt.GetString()!;
            if (type == "object" && schemaElt.TryGetProperty("additionalProperties", out JsonElement addlPropsElt) && addlPropsElt.ValueKind == JsonValueKind.Object)
            {
                CollateSchemaTypes(docName, defKey, addlPropsElt, schemaTypes, rootElementsByName);
                return;
            }
            else if (type == "array" && schemaElt.TryGetProperty("items", out JsonElement itemsElt) && itemsElt.ValueKind == JsonValueKind.Object)
            {
                CollateSchemaTypes(docName, defKey, itemsElt, schemaTypes, rootElementsByName);
                return;
            }

            string? title = schemaElt.TryGetProperty("title", out JsonElement titleElt) ? titleElt.GetString() : null;
            CodeName schemaName = new CodeName(this.typeNamer.GenerateTypeName(docName, defKey, title));

            string? description = schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null;

            if (schemaElt.TryGetProperty("properties", out JsonElement propertiesElt) && typeElt.GetString() == "object")
            {
                foreach (JsonProperty objProp in propertiesElt.EnumerateObject())
                {
                    CollateSchemaTypes(docName, objProp.Name, objProp.Value, schemaTypes, rootElementsByName);
                }

                HashSet<string> indirectFields = schemaElt.TryGetProperty("x-indirect", out JsonElement indirectElt) ? indirectElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                HashSet<string> requiredFields = schemaElt.TryGetProperty("required", out JsonElement requiredElt) ? requiredElt.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : new HashSet<string>();
                schemaTypes.Add(new ObjectType(
                    schemaName,
                    description,
                    propertiesElt.EnumerateObject().ToDictionary(p => new CodeName(p.Name), p => GetObjectTypeFieldInfo(docName, p.Name, p.Value, indirectFields, requiredFields, rootElementsByName)), orNull: false));
            }
            else if (schemaElt.TryGetProperty("enum", out JsonElement enumElt))
            {
                CodeName[] enumValues = enumElt.EnumerateArray().Select(e => new CodeName(e.GetString()!)).ToArray();
                schemaTypes.Add(new EnumType(
                    schemaName,
                    description,
                    enumValues,
                    orNull: false));
            }
        }

        private ObjectType.FieldInfo GetObjectTypeFieldInfo(string docName, string fieldName, JsonElement schemaElt, HashSet<string> indirectFields, HashSet<string> requiredFields, Dictionary<string, JsonElement> rootElementsByName)
        {
            bool isIndirect = indirectFields.Contains(fieldName);
            bool isRequired = requiredFields.Contains(fieldName);
            return new ObjectType.FieldInfo(
                GetSchemaTypeFromJsonElement(docName, fieldName, schemaElt, rootElementsByName, isOptional: !isRequired),
                isIndirect,
                isRequired,
                schemaElt.TryGetProperty("description", out JsonElement descElt) ? descElt.GetString() : null,
                schemaElt.TryGetProperty("index", out JsonElement indexElt) ? indexElt.GetInt32() : null);
        }

        private SchemaType GetSchemaTypeFromJsonElement(string docName, string keyName, JsonElement schemaElt, Dictionary<string, JsonElement> rootElementsByName, bool isOptional)
        {
            bool orNull = TryGetNestedNullableJsonElement(ref schemaElt) || isOptional;

            if (!schemaElt.TryGetProperty("$ref", out JsonElement referencingElt))
            {
                if (schemaElt.TryGetProperty("type", out JsonElement typeElt))
                {
                    string? title = schemaElt.TryGetProperty("title", out JsonElement titleElt) ? titleElt.GetString() : null;
                    CodeName schemaName = new CodeName(this.typeNamer.GenerateTypeName(docName, keyName, title));

                    if (typeElt.GetString() == "object" && (!schemaElt.TryGetProperty("additionalProperties", out JsonElement addlPropsElt) || addlPropsElt.ValueKind == JsonValueKind.False))
                    {
                        return new ReferenceType(schemaName, isNullable: true, orNull: orNull);
                    }
                    else if (typeElt.GetString() == "string" && schemaElt.TryGetProperty("enum", out _))
                    {
                        return new ReferenceType(schemaName, isNullable: false, orNull: orNull);
                    }
                }

                return GetPrimitiveTypeFromJsonElement(docName, keyName, schemaElt, orNull, rootElementsByName);
            }

            GetReferenceInfo(docName, referencingElt, rootElementsByName, out string refName, out string? refKey, out JsonElement refElt, out string? refTitle);
            CodeName referencedName = new CodeName(this.typeNamer.GenerateTypeName(refName, refKey, refTitle));

            if (refElt.TryGetProperty("properties", out _) || refElt.TryGetProperty("enum", out _))
            {
                string type = refElt.GetProperty("type").GetString()!;
                return new ReferenceType(referencedName, type == "object", orNull);
            }

            return GetPrimitiveTypeFromJsonElement(docName, keyName, refElt, orNull, rootElementsByName);
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

        private SchemaType GetPrimitiveTypeFromJsonElement(string docName, string keyName, JsonElement schemaElt, bool orNull, Dictionary<string, JsonElement> rootElementsByName)
        {
            switch (schemaElt.GetProperty("type").GetString())
            {
                case "array":
                    return new ArrayType(GetSchemaTypeFromJsonElement(docName, keyName, schemaElt.GetProperty("items"), rootElementsByName, isOptional: false), orNull);
                case "object":
                    JsonElement typeAndAddendaElt = schemaElt.GetProperty("additionalProperties");
                    return new MapType(GetSchemaTypeFromJsonElement(docName, keyName, typeAndAddendaElt, rootElementsByName, isOptional: false), orNull);
                case "boolean":
                    return new BooleanType(orNull);
                case "number":
                    return schemaElt.GetProperty("format").GetString() == "float" ? new FloatType(orNull) : new DoubleType(orNull);
                case "integer":
                    return schemaElt.GetProperty("maximum").GetUInt64() switch
                    {
                        < 128 => new ByteType(orNull),
                        < 256 => new UnsignedByteType(orNull),
                        < 32768 => new ShortType(orNull),
                        < 65536 => new UnsignedShortType(orNull),
                        < 2147483648 => new IntegerType(orNull),
                        < 4294967296 => new UnsignedIntegerType(orNull),
                        < 9223372036854775808 => new LongType(orNull),
                        _ => new UnsignedLongType(orNull),
                    };
                case "string":
                    if (schemaElt.TryGetProperty("format", out JsonElement formatElt))
                    {
                        return formatElt.GetString() switch
                        {
                            "date" => new DateType(orNull),
                            "date-time" => new DateTimeType(orNull),
                            "time" => new TimeType(orNull),
                            "duration" => new DurationType(orNull),
                            "uuid" => new UuidType(orNull),
                            _ => throw new Exception($"unrecognized 'string' schema (format = {formatElt.GetString()})"),
                        };
                    }
                    if (schemaElt.TryGetProperty("contentEncoding", out JsonElement encodingElt))
                    {
                        return encodingElt.GetString() switch
                        {
                            "base64" => new BytesType(orNull),
                            _ => throw new Exception($"unrecognized 'string' schema (contentEncoding = {encodingElt.GetString()})"),
                        };
                    }
                    if (schemaElt.TryGetProperty("pattern", out JsonElement patternElt))
                    {
                        return patternElt.GetString() switch
                        {
                            "^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$" => new DecimalType(orNull),
                            _ => throw new Exception($"unrecognized 'string' schema (pattern = {patternElt.GetString()})"),
                        };
                    }
                    return new StringType(orNull);
                default:
                    throw new Exception($"unrecognized schema (type = {schemaElt.GetProperty("type").GetString()})");
            }
        }

        private void GetReferenceInfo(
            string docName,
            JsonElement referencingElt,
            Dictionary<string, JsonElement> rootElementsByName,
            out string referencedName,
            out string? referencedKey,
            out JsonElement referencedElt,
            out string? referencedTitle)
        {
            string refString = Uri.UnescapeDataString(referencingElt.GetString()!);
            int fragIx = refString.IndexOf('#');

            string baseRef = fragIx switch
            {
                < 0 => refString,
                > 0 => refString.Substring(0, fragIx),
                0 => docName,
            };
            string fragment = fragIx < 0 ? string.Empty : refString.Substring(fragIx + 2);

            referencedName = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(docName)!, baseRef)).Replace('\\', '/');
            JsonElement rootElt = rootElementsByName[referencedName];

            int sepIx = fragment.IndexOf('/');
            string? refCollection = sepIx > 0 ? fragment.Substring(0, sepIx) : null;
            referencedKey = sepIx > 0 ? fragment.Substring(sepIx + 1) : null;

            referencedElt = referencedKey != null ? rootElt.GetProperty(refCollection!).GetProperty(referencedKey) : rootElt;
            referencedTitle = referencedElt.GetProperty("title").GetString()!;
        }
    }
}
