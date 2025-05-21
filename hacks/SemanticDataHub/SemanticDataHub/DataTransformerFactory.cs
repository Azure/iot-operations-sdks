namespace SemanticDataHub
{
    using System;
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
                    if (elt.GetArrayLength() != 2)
                    {
                        throw new Exception($"Invalid structure in binding {bindingFileName}: array must have exactly 2 elements");
                    }

                    JsonElement.ArrayEnumerator jsonElements = elt.EnumerateArray();
                    jsonElements.MoveNext();
                    JsonElement elt1 = jsonElements.Current;

                    if (elt1.ValueKind != JsonValueKind.String)
                    {
                        throw new Exception($"Invalid structure in binding {bindingFileName}; array first element must be string");
                    }

                    jsonElements.MoveNext();
                    JsonElement elt2 = jsonElements.Current;

                    if (elt2.ValueKind == JsonValueKind.String)
                    {
                        return new ConversionTransformer(propertyPath, elt1, elt2, bindingFileName);
                    }
                    else if (elt2.ValueKind == JsonValueKind.Object)
                    {
                        return new MappingTransformer(elt1, elt2, bindingFileName);
                    }
                    else
                    {
                        throw new Exception($"Invalid structure in binding {bindingFileName}; array second element must be string or object");
                    }
                default:
                    throw new Exception($"Invalid structure in binding {bindingFileName}: property value must be object, string, or array");
            }
        }
    }
}
