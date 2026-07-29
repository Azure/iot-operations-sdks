namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using DTDLParser;
    using DTDLParser.Models;

    public class ThingDescriber
    {
        private readonly int mqttVersion;
        private HashSet<Dtmi> ancestralSchemaIds;

        public ThingDescriber(int mqttVersion)
        {
            this.mqttVersion = mqttVersion;
            this.ancestralSchemaIds = new HashSet<Dtmi>();
        }

        public string GetTypeAndAddenda(DTSchemaInfo dtSchema, int indent)
        {
            if (dtSchema.EntityKind == DTEntityKind.Object)
            {
                var templateTransform = new ObjectThingSchema((DTObjectInfo)dtSchema, indent, this.mqttVersion, this);
                return this.GetTransformedText(templateTransform, dtSchema.Id);
            }

            if (dtSchema.EntityKind == DTEntityKind.Enum)
            {
                var templateTransform = new EnumThingSchema((DTEnumInfo)dtSchema, indent);
                return this.GetTransformedText(templateTransform, dtSchema.Id);
            }

            if (dtSchema.EntityKind == DTEntityKind.Array)
            {
                var templateTransform = new ArrayThingSchema((DTArrayInfo)dtSchema, indent, this);
                return this.GetTransformedText(templateTransform, dtSchema.Id);
            }

            if (dtSchema.EntityKind == DTEntityKind.Map)
            {
                var templateTransform = new MapThingSchema((DTMapInfo)dtSchema, indent, this);
                return this.GetTransformedText(templateTransform, dtSchema.Id);
            }

            string it = new string(' ', indent);
            string nl = $"{Environment.NewLine}{it}";

            return dtSchema.Id.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => $"{it}\"type\": \"boolean\"",
                "dtmi:dtdl:instance:Schema:double;2" => $"{it}\"type\": \"number\",{nl}\"minimum\": -1.80e+308,{nl}\"maximum\": 1.80e+308",
                "dtmi:dtdl:instance:Schema:float;2" => $"{it}\"type\": \"number\",{nl}\"minimum\": -3.40e+38,{nl}\"maximum\": 3.40e+38",
                "dtmi:dtdl:instance:Schema:integer;2" => $"{it}\"type\": \"integer\",{nl}\"minimum\": -2147483648,{nl}\"maximum\": 2147483647",
                "dtmi:dtdl:instance:Schema:long;2" => $"{it}\"type\": \"integer\",{nl}\"minimum\": -9223372036854775808,{nl}\"maximum\": 9223372036854775807",
                "dtmi:dtdl:instance:Schema:byte;4" => $"{it}\"type\": \"integer\",{nl}\"minimum\": -128,{nl}\"maximum\": 127",
                "dtmi:dtdl:instance:Schema:short;4" => $"{it}\"type\": \"integer\",{nl}\"minimum\": -32768,{nl}\"maximum\": 32767",
                "dtmi:dtdl:instance:Schema:unsignedInteger;4" => $"{it}\"type\": \"integer\",{nl}\"minimum\": 0,{nl}\"maximum\": 4294967295",
                "dtmi:dtdl:instance:Schema:unsignedLong;4" => $"{it}\"type\": \"integer\",{nl}\"minimum\": 0,{nl}\"maximum\": 18446744073709551615",
                "dtmi:dtdl:instance:Schema:unsignedByte;4" => $"{it}\"type\": \"integer\",{nl}\"minimum\": 0,{nl}\"maximum\": 255",
                "dtmi:dtdl:instance:Schema:unsignedShort;4" => $"{it}\"type\": \"integer\",{nl}\"minimum\": 0,{nl}\"maximum\": 65535",
                "dtmi:dtdl:instance:Schema:date;2" => $"{it}\"type\": \"string\",{nl}\"format\": \"date\"",
                "dtmi:dtdl:instance:Schema:dateTime;2" => $"{it}\"type\": \"string\",{nl}\"format\": \"date-time\"",
                "dtmi:dtdl:instance:Schema:time;2" => $"{it}\"type\": \"string\",{nl}\"format\": \"time\"",
                "dtmi:dtdl:instance:Schema:duration;2" => $"{it}\"type\": \"string\",{nl}\"pattern\": " + @"""^P(?!$)(?:(?:(?:(?:\\d+Y)|(?:\\d+\\.\\d+Y$))?(?:(?:\\d+M)|(?:\\d+\\.\\d+M$))?)|(?:(?:(?:\\d+W)|(?:\\d+\\.\\d+W$))?))(?:(?:\\d+D)|(?:\\d+\\.\\d+D$))?(?:T(?!$)(?:(?:\\d+H)|(?:\\d+\\.\\d+H$))?(?:(?:\\d+M)|(?:\\d+\\.\\d+M$))?(?:\\d+(?:\\.\\d+)?S)?)?$""",
                "dtmi:dtdl:instance:Schema:string;2" => $"{it}\"type\": \"string\"",
                "dtmi:dtdl:instance:Schema:uuid;4" => $"{it}\"type\": \"string\",{nl}\"format\": \"uuid\"",
                "dtmi:dtdl:instance:Schema:bytes;4" => $"{it}\"type\": \"string\",{nl}\"contentEncoding\": \"base64\"",
                "dtmi:dtdl:instance:Schema:decimal;4" => $"{it}\"type\": \"string\",{nl}\"pattern\": " + @"""^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$""",
                _ => string.Empty,
            };
        }

        public string GetCommandAffordance(DTCommandInfo dtCommand, bool usesTypes, string contentType, string commandTopic, string serviceGroupId)
        {
            var templateTransform = new CommandAffordance(dtCommand, this.mqttVersion, usesTypes, contentType, commandTopic, serviceGroupId, this);
            return templateTransform.TransformText();
        }

        public string GetPropertyAffordance(DTPropertyInfo dtProperty, bool usesTypes, string contentType, string propertyTopic)
        {
            var templateTransform = new PropertyAffordance(dtProperty, this.mqttVersion, usesTypes, contentType, propertyTopic, this);
            return templateTransform.TransformText();
        }

        public string GetTelemetryAffordance(DTTelemetryInfo dtTelemetry, bool usesTypes, string contentType, string telemetryTopic, string serviceGroupId)
        {
            var templateTransform = new TelemetryAffordance(dtTelemetry, usesTypes, contentType, telemetryTopic, serviceGroupId, this);
            return templateTransform.TransformText();
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

        private string GetTransformedText(ITemplateTransform templateTransform, Dtmi schemaId)
        {
            if (this.ancestralSchemaIds.Contains(schemaId))
            {
                throw new RecursionException(new CodeName(schemaId));
            }

            this.ancestralSchemaIds.Add(schemaId);
            string text = templateTransform.TransformText();
            this.ancestralSchemaIds.Remove(schemaId);

            return text;
        }
    }
}
