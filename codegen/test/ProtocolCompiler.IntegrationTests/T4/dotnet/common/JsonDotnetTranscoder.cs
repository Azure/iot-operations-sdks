using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;

namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public class JsonDotnetTranscoder : IDotnetTranscoder
    {
        private int iterIx = 0;

        public bool CapitalizerNeeded { get; internal set; } = false;

        public bool DecapitalizerNeeded { get; internal set; } = false;

        public string EmptySchemaType { get; }

        public JsonDotnetTranscoder(string emptySchemaType)
        {
            EmptySchemaType = emptySchemaType;
        }

        public string CheckPresence(string objName, string fieldName, SchemaTypeInfo schemaType)
        {
            return $"{objName}.{Capitalize(fieldName)} != null";
        }

        public string JTokenFromSchemaField(string objName, string fieldName, SchemaTypeInfo schemaType)
        {
            return JTokenFromSchemaValue($"{objName}.{Capitalize(fieldName)}", schemaType);
        }

        public string AssignSchemaFieldFromJToken(string objName, string fieldName, string varName, SchemaTypeInfo schemaType)
        {
            return $"{objName}.{Capitalize(fieldName)} = {SchemaValueFromJToken(varName, schemaType)};";
        }

        public string JTokenFromSchemaValue(string varName, SchemaTypeInfo schemaType)
        {
            string iter;
            switch (schemaType)
            {
                case PrimitiveTypeInfo primitiveType:
                    return primitiveType.SchemaName switch
                    {
                        "Boolean" => $"new JValue({varName})",
                        "Date" => $"new JValue({varName}?.ToString(\"yyyy-MM-dd\"))",
                        "DateTime" => $"new JValue({varName}?.ToString(\"yyyy-MM-ddTHH:mm:ss\"))",
                        "Double" => $"new JValue({varName})",
                        "Duration" => $"new JValue(System.Xml.XmlConvert.ToString((TimeSpan){varName}))",
                        "Float" => $"new JValue({varName})",
                        "Integer" => $"new JValue({varName})",
                        "Long" => $"new JValue({varName})",
                        "String" => $"new JValue({varName})",
                        "Time" => $"new JValue({varName}?.ToString(\"HH:mm:ss\"))",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    iter = GetIter();
                    return $"new JArray({varName}.Select({iter} => {JTokenFromSchemaValue(iter, arrayType.ElementSchema)}))";
                case ObjectTypeInfo objectType:
                    return $"new JObject(new List<JProperty> {{ {string.Join(", ", objectType.FieldSchemas.Select(f => $"new JProperty(\"{f.Key}\", {JTokenFromSchemaValue($"{varName}.{Capitalize(f.Key)}", f.Value)})"))} }})";
                case MapTypeInfo mapType:
                    iter = GetIter();
                    return $"new JObject({varName}.Select({iter} => new JProperty({iter}.Key, {JTokenFromSchemaValue($"{iter}.Value", mapType.ValueSchema)})))";
                case EnumTypeInfo enumType:
                    return $"{schemaType.SchemaName}_Tokenizer.EnumToJToken({varName})";
                default: throw new Exception($"inappropriate schema type {schemaType.GetType()}");
            }
        }

        public string SchemaValueFromJToken(string varName, SchemaTypeInfo schemaType)
        {
            string iter;
            switch (schemaType)
            {
                case PrimitiveTypeInfo primitiveType:
                    return primitiveType.SchemaName switch
                    {
                        "Boolean" => $"((JValue){varName}).Value<bool>()",
                        "Date" => $"DateOnly.ParseExact(((JValue){varName}).Value<string>(), \"yyyy-MM-dd\")",
                        "DateTime" => $"DateTime.ParseExact(((JValue){varName}).Value<string>(), \"yyyy-MM-ddTHH:mm:ss\", null)",
                        "Double" => $"((JValue){varName}).Value<double>()",
                        "Duration" => $"System.Xml.XmlConvert.ToTimeSpan(((JValue){varName}).Value<string>())",
                        "Float" => $"((JValue){varName}).Value<float>()",
                        "Integer" => $"((JValue){varName}).Value<int>()",
                        "Long" => $"((JValue){varName}).Value<long>()",
                        "String" => $"((JValue){varName}).Value<string>()",
                        "Time" => $"TimeOnly.ParseExact(((JValue){varName}).Value<string>(), \"HH:mm:ss\")",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    iter = GetIter();
                    return $"((JArray){varName}).Select({iter} => {SchemaValueFromJToken(iter, arrayType.ElementSchema)}).ToList()";
                case ObjectTypeInfo objectType:
                    return $"new {objectType.SchemaName} {{ {string.Join(", ", objectType.FieldSchemas.Select(f => $"{Capitalize(f.Key)} = {SchemaValueFromJToken($"((JObject){varName})[\"{f.Key}\"]", f.Value)}"))} }}";
                case MapTypeInfo mapType:
                    iter = GetIter();
                    return $"((JObject){varName}).Properties().ToDictionary({iter} => {iter}.Name, {iter} => {SchemaValueFromJToken($"{iter}.Value", mapType.ValueSchema)})";
                case EnumTypeInfo enumType:
                    return $"{enumType.SchemaName}_Tokenizer.JTokenToEnum({varName})";
                default: throw new Exception($"inappropriate schema type {schemaType.GetType()}");
            }
        }

        private string GetIter() => $"i{++iterIx}";

        private static string Capitalize(string inString) => char.ToUpperInvariant(inString[0]) + inString.Substring(1);

        private static string Decapitalize(string inString) => char.ToLowerInvariant(inString[0]) + inString.Substring(1);
    }
}
