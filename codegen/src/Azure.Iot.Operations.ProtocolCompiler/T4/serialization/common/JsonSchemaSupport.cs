namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public static class JsonSchemaSupport
    {
        public static string GetTypeAndAddenda(DTSchemaInfo dtSchema)
        {
            if (dtSchema.EntityKind == DTEntityKind.Array)
            {
                return $"\"type\": \"array\", \"items\": {{ {GetTypeAndAddenda(((DTArrayInfo)dtSchema).ElementSchema)} }}";
            }

            if (dtSchema.EntityKind == DTEntityKind.Map)
            {
                return $"\"type\": \"object\", \"additionalProperties\": {{ {GetTypeAndAddenda(((DTMapInfo)dtSchema).MapValue.Schema)} }}";
            }

            return dtSchema.Id.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => @"""type"": ""boolean""",
                "dtmi:dtdl:instance:Schema:double;2" => @"""type"": ""number"", ""format"": ""double""",
                "dtmi:dtdl:instance:Schema:float;2" => @"""type"": ""number"", ""format"": ""float""",
                "dtmi:dtdl:instance:Schema:integer;2" => @"""type"": ""integer"", ""minimum"": -2147483648, ""maximum"": 2147483647",
                "dtmi:dtdl:instance:Schema:long;2" => @"""type"": ""integer"", ""minimum"": -9223372036854775808, ""maximum"": 9223372036854775807",
                "dtmi:dtdl:instance:Schema:byte;4" => @"""type"": ""integer"", ""minimum"": -128, ""maximum"": 127",
                "dtmi:dtdl:instance:Schema:short;4" => @"""type"": ""integer"", ""minimum"": -32768, ""maximum"": 32767",
                "dtmi:dtdl:instance:Schema:unsignedInteger;4" => @"""type"": ""integer"", ""minimum"": 0, ""maximum"": 4294967295",
                "dtmi:dtdl:instance:Schema:unsignedLong;4" => @"""type"": ""integer"", ""minimum"": 0, ""maximum"": 18446744073709551615",
                "dtmi:dtdl:instance:Schema:unsignedByte;4" => @"""type"": ""integer"", ""minimum"": 0, ""maximum"": 255",
                "dtmi:dtdl:instance:Schema:unsignedShort;4" => @"""type"": ""integer"", ""minimum"": 0, ""maximum"": 65535",
                "dtmi:dtdl:instance:Schema:date;2" => @"""type"": ""string"", ""format"": ""date""",
                "dtmi:dtdl:instance:Schema:dateTime;2" => @"""type"": ""string"", ""format"": ""date-time""",
                "dtmi:dtdl:instance:Schema:time;2" => @"""type"": ""string"", ""format"": ""time""",
                "dtmi:dtdl:instance:Schema:duration;2" => @"""type"": ""string"", ""format"": ""duration""",
                "dtmi:dtdl:instance:Schema:string;2" => @"""type"": ""string""",
                "dtmi:dtdl:instance:Schema:uuid;4" => @"""type"": ""string"", ""format"": ""uuid""",
                "dtmi:dtdl:instance:Schema:bytes;4" => @"""type"": ""string"", ""contentEncoding"": ""base64""",
                "dtmi:dtdl:instance:Schema:decimal;4" => @"""type"": ""string"", ""pattern"": ""^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$""",
                _ => $"\"$ref\": \"{new CodeName(dtSchema.Id).GetFileName(TargetLanguage.Independent)}.schema.json\"",
            };
        }

        public static string GetPrimitiveType(Dtmi primitiveSchemaId)
        {
            return primitiveSchemaId.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => "boolean",
                "dtmi:dtdl:instance:Schema:double;2" => "number",
                "dtmi:dtdl:instance:Schema:float;2" => "number",
                "dtmi:dtdl:instance:Schema:integer;2" => "integer",
                "dtmi:dtdl:instance:Schema:long;2" => "integer",
                "dtmi:dtdl:instance:Schema:byte;4" => "integer",
                "dtmi:dtdl:instance:Schema:short;4" => "integer",
                "dtmi:dtdl:instance:Schema:unsignedInteger;4" => "integer",
                "dtmi:dtdl:instance:Schema:unsignedLong;4" => "integer",
                "dtmi:dtdl:instance:Schema:unsignedByte;4" => "integer",
                "dtmi:dtdl:instance:Schema:unsignedShort;4" => "integer",
                "dtmi:dtdl:instance:Schema:date;2" => "string",
                "dtmi:dtdl:instance:Schema:dateTime;2" => "string",
                "dtmi:dtdl:instance:Schema:time;2" => "string",
                "dtmi:dtdl:instance:Schema:duration;2" => "string",
                "dtmi:dtdl:instance:Schema:string;2" => "string",
                "dtmi:dtdl:instance:Schema:uuid;4" => "string",
                "dtmi:dtdl:instance:Schema:bytes;4" => "string",
                "dtmi:dtdl:instance:Schema:decimal;4" => "string",
                _ => "null",
            };
        }
    }
}
