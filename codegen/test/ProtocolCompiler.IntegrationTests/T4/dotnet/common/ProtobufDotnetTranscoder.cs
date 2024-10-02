namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;
    using System.Collections.Generic;

    public class ProtobufDotnetTranscoder : IDotnetTranscoder
    {
        private static readonly HashSet<string> TruePrimitives = new()
        {
            "Boolean",
            "Double",
            "Float",
            "Integer",
            "Long",
            "String",
        };

        private int iterIx = 0;

        public bool CapitalizerNeeded { get; internal set; } = false;

        public bool DecapitalizerNeeded { get; internal set; } = false;

        public string EmptySchemaType { get => "Empty"; }

        public string CheckPresence(string objName, string fieldName, SchemaTypeInfo schemaType)
        {
            return schemaType is EnumTypeInfo || schemaType is PrimitiveTypeInfo && TruePrimitives.Contains(schemaType.SchemaName) ?
                $"{objName}.Has{ProtoName(fieldName)}" :
                $"{objName}.{ProtoName(fieldName)} != null";
        }

        public string JTokenFromSchemaField(string objName, string fieldName, SchemaTypeInfo schemaType)
        {
            return JTokenFromSchemaValue($"{objName}.{ProtoName(fieldName)}", schemaType);
        }

        public string AssignSchemaFieldFromJToken(string objName, string fieldName, string varName, SchemaTypeInfo schemaType)
        {
            return $"{objName}.{ProtoName(fieldName)} = {SchemaValueFromJToken(varName, schemaType)};";
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
                        "Date" => $"new JValue({varName}?.ToDateTime().ToString(\"yyyy-MM-dd\"))",
                        "DateTime" => $"new JValue({varName}?.ToDateTime().ToString(\"yyyy-MM-ddTHH:mm:ss\"))",
                        "Double" => $"new JValue({varName})",
                        "Duration" => $"new JValue(System.Xml.XmlConvert.ToString({varName}.ToTimeSpan()))",
                        "Float" => $"new JValue({varName})",
                        "Integer" => $"new JValue({varName})",
                        "Long" => $"new JValue({varName})",
                        "String" => $"new JValue({varName})",
                        "Time" => $"new JValue({varName}?.ToDateTime().ToString(\"HH:mm:ss\"))",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    iter = GetIter();
                    return $"new JArray({varName}.Value.Select({iter} => {JTokenFromSchemaValue(iter, arrayType.ElementSchema)}))";
                case ObjectTypeInfo objectType:
                    return $"new JObject(new List<JProperty> {{ {string.Join(", ", objectType.FieldSchemas.Select(f => $"new JProperty(\"{f.Key}\", {JTokenFromSchemaValue($"{varName}.{ProtoName(f.Key)}", f.Value)})"))} }})";
                case MapTypeInfo mapType:
                    iter = GetIter();
                    return $"new JObject({varName}.Value.Select({iter} => new JProperty({iter}.Key, {JTokenFromSchemaValue($"{iter}.Value", mapType.ValueSchema)})))";
                case EnumTypeInfo enumType:
                    return $"new JValue(typeof({enumType.SchemaName}).GetField(System.Enum.GetName(typeof({enumType.SchemaName}), {varName})).GetCustomAttributes(false).OfType<OriginalNameAttribute>().Single().Name)";
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
                        "Date" => $"DateOnly.ParseExact(((JValue){varName}).Value<string>(), \"yyyy-MM-dd\").ToDateTime(new TimeOnly(0, 0, 0), DateTimeKind.Utc).ToTimestamp()",
                        "DateTime" => $"DateTime.ParseExact(((JValue){varName}).Value<string>(), \"yyyy-MM-ddTHH:mm:ss\", null, DateTimeStyles.AssumeUniversal).ToUniversalTime().ToTimestamp()",
                        "Double" => $"((JValue){varName}).Value<double>()",
                        "Duration" => $"System.Xml.XmlConvert.ToTimeSpan(((JValue){varName}).Value<string>()).ToDuration()",
                        "Float" => $"((JValue){varName}).Value<float>()",
                        "Integer" => $"((JValue){varName}).Value<int>()",
                        "Long" => $"((JValue){varName}).Value<long>()",
                        "String" => $"((JValue){varName}).Value<string>()",
                        "Time" => $"new DateOnly(1, 1, 1).ToDateTime(TimeOnly.ParseExact(((JValue){varName}).Value<string>(), \"HH:mm:ss\"), DateTimeKind.Utc).ToTimestamp()",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    iter = GetIter();
                    return $"new {arrayType.SchemaName} {{ Value = {{ ((JArray){varName}).Select({iter} => {SchemaValueFromJToken(iter, arrayType.ElementSchema)}) }} }}";
                case ObjectTypeInfo objectType:
                    return $"new {objectType.SchemaName} {{ {string.Join(", ", objectType.FieldSchemas.Select(f => $" {ProtoName(f.Key)} = {SchemaValueFromJToken($"((JObject){varName})[\"{f.Key}\"]", f.Value)}"))} }}";
                case MapTypeInfo mapType:
                    iter = GetIter();
                    return $"new {mapType.SchemaName} {{ Value = {{ ((JObject){varName}).Properties().ToDictionary({iter} => {iter}.Name, {iter} => {SchemaValueFromJToken($"{iter}.Value", mapType.ValueSchema)}) }} }}";
                case EnumTypeInfo enumType:
                    return $"System.Enum.GetValues<{enumType.SchemaName}>().First(e => typeof({enumType.SchemaName}).GetField(System.Enum.GetName(typeof({enumType.SchemaName}), e)).GetCustomAttributes(false).OfType<OriginalNameAttribute>().Single().Name == ((JValue){varName}).Value<string>())";
                default: throw new Exception($"inappropriate schema type {schemaType.GetType()}");
            }
        }

        private string GetIter() => $"i{++iterIx}";

        private static string ProtoName(string inString) => string.Concat(inString.Select((c, i) => i == 0 || !char.IsLetter(inString, i - 1) ? char.ToUpperInvariant(c) : c).Where(u => u != '_'));
    }
}
