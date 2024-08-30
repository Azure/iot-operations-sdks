namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Xml;

    public class ObjectPropertyRowGenerator
    {
        private readonly string suiteName;
        private readonly DefaultValues defaultValues;
        private readonly JsonDocument objectSchema;

        public ObjectPropertyRowGenerator(XmlElement tableElt, JsonSchemata jsonSchemata, DefaultValues defaultValues, string suiteName, string itemName)
        {
            this.defaultValues = defaultValues;
            this.suiteName = suiteName;
            string objectName = tableElt.HasAttribute("object") ? tableElt.GetAttribute("object") : itemName;
            objectSchema = jsonSchemata.GetSchema(objectName);
        }

        public void GenerateRows(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns, string? defaultPath)
        {
            List<string> requiredProperties = new();
            if (objectSchema.RootElement.TryGetProperty("required", out JsonElement requiredElt))
            {
                requiredProperties = requiredElt.EnumerateArray().Select(e => e.GetString()!).ToList();
            }

            JsonElement propertiesElt = objectSchema.RootElement.GetProperty("properties");
            foreach (JsonProperty prop in propertiesElt.EnumerateObject())
            {
                this.GenerateRow(markdownFile, columns, prop, defaultPath, requiredProperties);
            }
        }

        private void GenerateRow(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns, JsonProperty prop, string? defaultPath, List<string> requiredProperties)
        {
            markdownFile.BeginTableRow();

            foreach (TableColumnSchema column in columns)
            {
                switch (column.Field)
                {
                    case "propertyName":
                        markdownFile.TableCell(prop.Name);
                        break;
                    case "kind":
                        markdownFile.TableCell(prop.Value.TryGetProperty("kind", out JsonElement kindElt) ? kindElt.ToString() : string.Empty);
                        break;
                    case "required":
                        markdownFile.TableCell(requiredProperties.Contains(prop.Name) ? column.TrueValue : column.FalseValue);
                        break;
                    case "type":
                        markdownFile.TableCell(GetTypeDescription(prop.Value));
                        break;
                    case "const":
                        markdownFile.TableCell(GetConstValue(prop.Value));
                        break;
                    case "default":
                        markdownFile.TableCell(defaultPath != null ? defaultValues.GetDefaultAsString(suiteName, defaultPath, prop.Name) ?? GetEmptyDefault(prop.Value) : GetEmptyDefault(prop.Value));
                        break;
                    default:
                        markdownFile.TableCell(prop.Value.GetProperty(column.Field).GetString() ?? string.Empty);
                        break;
                }
            }

            markdownFile.EndTableRow();
        }

        private static string GetEmptyDefault(JsonElement elt)
        {
            if (elt.TryGetProperty("const", out JsonElement _))
            {
                return string.Empty;
            }
            else if(elt.TryGetProperty("type", out JsonElement typeElt))
            {
                if (typeElt.ValueKind == JsonValueKind.Array && typeElt.EnumerateArray().Any(e => e.GetString() == "null"))
                {
                    return "null";
                }
                else if (typeElt.ValueKind == JsonValueKind.String)
                {
                    return typeElt.GetString() switch
                    {
                        "boolean" => "false",
                        "intetger" => "0",
                        "string" => string.Empty,
                        "array" => "[ ]",
                        "object" => "{ }",
                        _ => string.Empty,
                    };
                }
                else
                {
                    return string.Empty;
                }
            }
            else if (elt.TryGetProperty("anyOf", out JsonElement anyOfElt) && anyOfElt.EnumerateArray().Any(e => e.TryGetProperty("type", out typeElt) && typeElt.GetString() == "null"))
            {
                return "null";
            }
            else if (elt.TryGetProperty("empty", out JsonElement emptyElt))
            {
                return emptyElt.GetString() ?? string.Empty;
            }
            else
            {
                return string.Empty;
            }
        }

        private static string GetTypeDescription(JsonElement elt)
        {
            if (elt.TryGetProperty("type", out JsonElement typeElt))
            {
                if (typeElt.ValueKind == JsonValueKind.Array)
                {
                    return string.Join(" or ", typeElt.EnumerateArray().Select(e => e.ToString()));
                }
                else
                {
                    return typeElt.ToString() switch
                    {
                        "array" => $"array of {GetTypeDescription(elt.GetProperty("items"))}",
                        "object" => GetMapType(elt),
                        _ => typeElt.ToString(),
                    };
                }
            }
            else if (elt.TryGetProperty("$ref", out JsonElement refElt))
            {
                string refFile = refElt.GetString()!;
                string refName = refFile.Substring(0, refFile.IndexOf('.'));
                return $"[{refName}](#{MarkdownFile.ToReference(refName)})";
            }
            else if (elt.TryGetProperty("anyOf", out JsonElement anyOfElt))
            {
                return string.Join(" or ", anyOfElt.EnumerateArray().Select(e => GetTypeDescription(e)));
            }
            else
            {
                return string.Empty;
            }
        }

        private static string GetMapType(JsonElement elt)
        {
            JsonElement additionalPropsElt = elt.GetProperty("additionalProperties");
            if (additionalPropsElt.ValueKind != JsonValueKind.False)
            {
                return $"map from string to {GetTypeDescription(additionalPropsElt)}";
            }
            else if (elt.TryGetProperty("patternProperties", out JsonElement patternPropsElt) && patternPropsElt.TryGetProperty("^\\d+$", out JsonElement regexElt))
            {
                return $"map from integer to {GetTypeDescription(regexElt)}";
            }
            else
            {
                return $"map";
            }
        }

        private static string GetConstValue(JsonElement elt)
        {
            if (elt.TryGetProperty("const", out JsonElement constElt))
            {
                return constElt.ValueKind == JsonValueKind.String ? $"\"{constElt}\"" : constElt.ToString();
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
