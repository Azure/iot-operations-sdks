namespace SemanticDataHub
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;

    internal static class DataTransformerFactory
    {
        public static IDataTransformer Create(string propertyPath, JsonElement elt, string bindingFileName)
        {
            switch (elt.ValueKind)
            {
                case JsonValueKind.Object:
                    return new StructuralTransformer(propertyPath, elt, bindingFileName);
                case JsonValueKind.String:
                    return new SelectionTransformer(elt, bindingFileName);
                case JsonValueKind.Array:
                    if (TryGet3Elements(elt, out JsonElement elt0, out JsonElement elt1, out JsonElement elt2))
                    {
                        if (elt0.ValueKind == JsonValueKind.String && elt0.GetString()!.StartsWith('@'))
                        {
                            switch (elt0.GetString()!)
                            {
                                case "@quant":
                                    return new ConversionTransformer(propertyPath, elt1, elt2, bindingFileName);
                                case "@map":
                                    return new MappingTransformer(elt1, elt2, bindingFileName);
                                default:
                                    throw new Exception($"unrecognized directive: '{elt0.GetString()}'");
                            }
                        }
                    }

                    return new ArrayTransformer(propertyPath, elt, bindingFileName);
                default:
                    throw new Exception($"Invalid structure in binding {bindingFileName}: property value must be object, string, or array");
            }
        }

        private static bool TryGet3Elements(JsonElement arrayElt, out JsonElement elt0, out JsonElement elt1, out JsonElement elt2)
        {
            if (arrayElt.GetArrayLength() != 3)
            {
                elt0 = default(JsonElement);
                elt1 = default(JsonElement);
                elt2 = default(JsonElement);
                return false;
            }

            JsonElement.ArrayEnumerator jsonElements = arrayElt.EnumerateArray();
            jsonElements.MoveNext();
            elt0 = jsonElements.Current;

            jsonElements.MoveNext();
            elt1 = jsonElements.Current;

            jsonElements.MoveNext();
            elt2 = jsonElements.Current;

            return true;
        }
    }
}
