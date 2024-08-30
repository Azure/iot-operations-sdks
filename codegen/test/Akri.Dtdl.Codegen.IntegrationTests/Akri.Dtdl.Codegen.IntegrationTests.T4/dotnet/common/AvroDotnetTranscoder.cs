using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;

namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public class AvroDotnetTranscoder : IDotnetTranscoder
    {
        private int iterIx = 0;

        public bool CapitalizerNeeded { get; internal set; } = false;

        public bool DecapitalizerNeeded { get; internal set; } = false;

        public string EmptySchemaType { get => "EmptyAvro"; }

        public string CheckPresence(string objName, string fieldName, SchemaTypeInfo schemaType)
        {
            return $"{objName}.{fieldName} != null";
        }

        public string JTokenFromSchemaField(string objName, string fieldName, SchemaTypeInfo schemaType)
        {
            return JTokenFromSchemaValue($"{objName}.{fieldName}", schemaType);
        }

        public string AssignSchemaFieldFromJToken(string objName, string fieldName, string varName, SchemaTypeInfo schemaType)
        {
            return $"{objName}.{fieldName} = {SchemaValueFromJToken(varName, schemaType)};";
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
                        "Duration" => $"new JValue({varName})",
                        "Float" => $"new JValue({varName})",
                        "Integer" => $"new JValue({varName})",
                        "Long" => $"new JValue({varName})",
                        "String" => $"new JValue({varName})",
                        "Time" => $"new JValue({varName}?.ToString(@\"hh\\:mm\\:ss\"))",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    iter = GetIter();
                    return $"new JArray({varName}.Select({iter} => {JTokenFromSchemaValue(iter, arrayType.ElementSchmema)}))";
                case ObjectTypeInfo objectType:
                    return $"new JObject(new List<JProperty> {{ {string.Join(", ", objectType.FieldSchemas.Select(f => $"new JProperty(\"{f.Key}\", {JTokenFromSchemaValue($"{varName}.{f.Key}", f.Value)})"))} }})";
                case MapTypeInfo mapType:
                    iter = GetIter();
                    return $"new JObject({varName}.Select({iter} => new JProperty({iter}.Key, {JTokenFromSchemaValue($"{iter}.Value", mapType.ValueSchmema)})))";
                case EnumTypeInfo enumType:
                    return $"new JValue({varName}?.ToString())";
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
                        "Date" => $"DateTime.ParseExact(((JValue){varName}).Value<string>(), \"yyyy-MM-dd\", null)",
                        "DateTime" => $"DateTime.ParseExact(((JValue){varName}).Value<string>(), \"yyyy-MM-ddTHH:mm:ss\", null, DateTimeStyles.AssumeUniversal)",
                        "Double" => $"((JValue){varName}).Value<double>()",
                        "Duration" => $"((JValue){varName}).Value<string>()",
                        "Float" => $"((JValue){varName}).Value<float>()",
                        "Integer" => $"((JValue){varName}).Value<int>()",
                        "Long" => $"((JValue){varName}).Value<long>()",
                        "String" => $"((JValue){varName}).Value<string>()",
                        "Time" => $"TimeOnly.ParseExact(((JValue){varName}).Value<string>(), \"HH:mm:ss\").ToTimeSpan()",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    iter = GetIter();
                    return $"({DotNetType(arrayType)})((JArray){varName}).Select({iter} => {SchemaValueFromJToken(iter, arrayType.ElementSchmema)}).ToList()";
                case ObjectTypeInfo objectType:
                    return $"new {objectType.SchemaName} {{ {string.Join(", ", objectType.FieldSchemas.Select(f => $"{f.Key} = {SchemaValueFromJToken($"((JObject){varName})[\"{f.Key}\"]", f.Value)}"))} }}";
                case MapTypeInfo mapType:
                    iter = GetIter();
                    return $"((JObject){varName}).Properties().ToDictionary({iter} => {iter}.Name, {iter} => {SchemaValueFromJToken($"{iter}.Value", mapType.ValueSchmema)})";
                case EnumTypeInfo enumType:
                    return $"({enumType.SchemaName})System.Enum.Parse(typeof({enumType.SchemaName}), ((JValue){varName}).Value<string>())";
                default: throw new Exception($"inappropriate schema type {schemaType.GetType()}");
            }
        }

        private string DotNetType(SchemaTypeInfo schemaType)
        {
            switch (schemaType)
            {
                case PrimitiveTypeInfo primitiveType:
                    return primitiveType.SchemaName switch
                    {
                        "Boolean" => "bool",
                        "Date" => $"DateTime",
                        "DateTime" => $"DateTime",
                        "Double" => $"double",
                        "Duration" => $"string",
                        "Float" => $"float",
                        "Integer" => $"int",
                        "Long" => $"long",
                        "String" => $"string",
                        "Time" => $"TimeSpan",
                        _ => throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}"),
                    };
                case ArrayTypeInfo arrayType:
                    return $"IList<{DotNetType(arrayType.ElementSchmema)}>";
                case ObjectTypeInfo objectType:
                    return objectType.SchemaName;
                case MapTypeInfo mapType:
                    return $"Dictionary<string, {DotNetType(mapType.ValueSchmema)}>";
                case EnumTypeInfo enumType:
                    return enumType.SchemaName;
                default: throw new Exception($"inappropriate schema type {schemaType.GetType()}");
            }
        }

        private string GetIter() => $"i{++iterIx}";
    }
}
