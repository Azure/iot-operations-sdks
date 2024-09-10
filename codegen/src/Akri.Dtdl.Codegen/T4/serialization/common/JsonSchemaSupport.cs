namespace Akri.Dtdl.Codegen
{
    using DTDLParser;
    using DTDLParser.Models;

    public static class JsonSchemaSupport
    {
        public static string GetTypeAndAddenda(DTSchemaInfo dtSchema, DtmiToSchemaName dtmiToSchemaName)
        {
            if (dtSchema.EntityKind == DTEntityKind.Array)
            {
                return $"\"type\": \"array\", \"items\": {{ {GetTypeAndAddenda(((DTArrayInfo)dtSchema).ElementSchema, dtmiToSchemaName)} }}";
            }

            if (dtSchema.EntityKind == DTEntityKind.Map)
            {
                return $"\"type\": \"object\", \"additionalProperties\": {{ {GetTypeAndAddenda(((DTMapInfo)dtSchema).MapValue.Schema, dtmiToSchemaName)} }}";
            }

            return dtSchema.Id.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => @"""type"": ""boolean""",
                "dtmi:dtdl:instance:Schema:double;2" => @"""type"": ""number"", ""format"": ""double""",
                "dtmi:dtdl:instance:Schema:float;2" => @"""type"": ""number"", ""format"": ""float""",
                "dtmi:dtdl:instance:Schema:integer;2" => @"""type"": ""integer"", ""format"": ""int32""",
                "dtmi:dtdl:instance:Schema:long;2" => @"""type"": ""integer"", ""format"": ""int64""",
                "dtmi:dtdl:instance:Schema:date;2" => @"""type"": ""string"", ""format"": ""date""",
                "dtmi:dtdl:instance:Schema:dateTime;2" => @"""type"": ""string"", ""format"": ""date-time""",
                "dtmi:dtdl:instance:Schema:time;2" => @"""type"": ""string"", ""format"": ""time""",
                "dtmi:dtdl:instance:Schema:duration;2" => @"""type"": ""string"", ""format"": ""duration""",
                "dtmi:dtdl:instance:Schema:string;2" => @"""type"": ""string""",
                "dtmi:dtdl:instance:Schema:uuid;4" => @"""type"": ""string"", ""format"": ""uuid""",
                _ => $"\"$ref\": \"{dtmiToSchemaName(dtSchema.Id, dtSchema.EntityKind.ToString())}.schema.json\"",
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
                "dtmi:dtdl:instance:Schema:date;2" => "string",
                "dtmi:dtdl:instance:Schema:dateTime;2" => "string",
                "dtmi:dtdl:instance:Schema:time;2" => "string",
                "dtmi:dtdl:instance:Schema:duration;2" => "string",
                "dtmi:dtdl:instance:Schema:string;2" => "string",
                "dtmi:dtdl:instance:Schema:uuid;4" => "string",
                _ => "null",
            };
        }
    }
}
