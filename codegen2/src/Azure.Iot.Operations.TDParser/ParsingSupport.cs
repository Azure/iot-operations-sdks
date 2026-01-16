namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public static class ParsingSupport
    {
        public static void CheckForDuplicatePropertyName(ref Utf8JsonReader reader, string propertyName, Dictionary<string, long> propertyNames, string objectDesc)
        {
            if (propertyNames.ContainsKey(propertyName))
            {
                while (reader.TokenType == JsonTokenType.PropertyName)
                {
                    reader.Read();
                    reader.Skip();
                    reader.Read();
                }

                throw new InvalidOperationException($"duplicate property name '{propertyName}' found in {objectDesc} object");
            }
        }
    }
}
