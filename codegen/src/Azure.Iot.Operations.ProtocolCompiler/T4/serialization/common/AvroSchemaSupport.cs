namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public static class AvroSchemaSupport
    {
        public static string GetTypeAndAddenda(DTSchemaInfo dtSchema, int indent, bool nullable, bool nestNamedType)
        {
            if (nullable)
            {
                var templateTransform = new NullableAvroSchema(dtSchema, indent);
                return templateTransform.TransformText();
            }

            if (dtSchema.EntityKind == DTEntityKind.Object)
            {
                var templateTransform = new ObjectAvroSchema(new CodeName(dtSchema.Id), ((DTObjectInfo)dtSchema).Fields.Select(f => (f.Name, f.Schema, IsRequired(f))).ToList(), indent + (nestNamedType ? 2 : 0));
                string code = templateTransform.TransformText();
                return nestNamedType ? NestCode(code, indent) : code;
            }

            if (dtSchema.EntityKind == DTEntityKind.Enum)
            {
                var templateTransform = new EnumAvroSchema(new CodeName(dtSchema.Id), ((DTEnumInfo)dtSchema).EnumValues.Select(v => v.Name).ToList(), indent + (nestNamedType ? 2 : 0));
                string code = templateTransform.TransformText();
                return nestNamedType ? NestCode(code, indent) : code;
            }

            if (dtSchema.EntityKind == DTEntityKind.Array)
            {
                var templateTransform = new ArrayAvroSchema(((DTArrayInfo)dtSchema).ElementSchema, indent + (nestNamedType ? 2 : 0));
                string code = templateTransform.TransformText();
                return nestNamedType ? NestCode(code, indent) : code;
            }

            if (dtSchema.EntityKind == DTEntityKind.Map)
            {
                var templateTransform = new MapAvroSchema(((DTMapInfo)dtSchema).MapValue.Schema, indent + (nestNamedType ? 2 : 0));
                string code = templateTransform.TransformText();
                return nestNamedType ? NestCode(code, indent) : code;
            }

            string it = new string(' ', indent);

            return dtSchema.Id.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => $"{it}\"type\": \"boolean\"",
                "dtmi:dtdl:instance:Schema:double;2" => $"{it}\"type\": \"double\"",
                "dtmi:dtdl:instance:Schema:float;2" => $"{it}\"type\": \"float\"",
                "dtmi:dtdl:instance:Schema:integer;2" => $"{it}\"type\": \"int\"",
                "dtmi:dtdl:instance:Schema:long;2" => $"{it}\"type\": \"long\"",
                "dtmi:dtdl:instance:Schema:byte;4" => $"{it}\"type\": \"int\"",
                "dtmi:dtdl:instance:Schema:short;4" => $"{it}\"type\": \"int\"",
                "dtmi:dtdl:instance:Schema:unsignedInteger;4" => $"{it}\"type\": \"int\"",
                "dtmi:dtdl:instance:Schema:unsignedLong;4" => $"{it}\"type\": \"long\"",
                "dtmi:dtdl:instance:Schema:unsignedByte;4" => $"{it}\"type\": \"int\"",
                "dtmi:dtdl:instance:Schema:unsignedShort;4" => $"{it}\"type\": \"int\"",
                "dtmi:dtdl:instance:Schema:date;2" => $"{it}\"type\": \"int\",\r\n{it}\"logicalType\": \"date\"",
                "dtmi:dtdl:instance:Schema:dateTime;2" => $"{it}\"type\": \"long\",\r\n{it}\"logicalType\": \"timestamp-millis\"",
                "dtmi:dtdl:instance:Schema:time;2" => $"{it}\"type\": \"int\",\r\n{it}\"logicalType\": \"time-millis\"",
                "dtmi:dtdl:instance:Schema:duration;2" => $"{it}\"type\": \"string\"",
                "dtmi:dtdl:instance:Schema:string;2" => $"{it}\"type\": \"string\"",
                "dtmi:dtdl:instance:Schema:uuid;4" => $"{it}\"type\": \"string\"",
                "dtmi:dtdl:instance:Schema:bytes;4" => $"{it}\"type\": \"bytes\"",
                "dtmi:dtdl:instance:Schema:decimal;4" => $"{it}\"type\": \"string\"",
                _ => string.Empty,
            };
        }

        private static string NestCode(string code, int indent)
        {
            string indentation = new string(' ', indent);
            return $"{indentation}\"type\": {{\r\n{code}\r\n{indentation}}}";
        }

        private static bool IsRequired(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Any(t => DtdlMqttExtensionValues.RequiredAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
        }
    }
}
