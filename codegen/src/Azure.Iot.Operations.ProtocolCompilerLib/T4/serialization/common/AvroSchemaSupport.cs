namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public static class AvroSchemaSupport
    {
        public static string GetTypeAndAddenda(DTSchemaInfo dtSchema, int indent, CodeName? sharedPrefix, bool nullable, HashSet<Dtmi> definedIds, int mqttVersion)
        {
            CodeName schemaId = new CodeName(dtSchema.Id);

            CodeName? sharedNamespace = CommonSchemaSupport.GetNamespace(dtSchema.Id, sharedPrefix);

            if (nullable)
            {
                var templateTransform = new NullableAvroSchema(dtSchema, indent, sharedPrefix, definedIds, mqttVersion);
                return templateTransform.TransformText();
            }

            if (definedIds.Contains(dtSchema.Id))
            {
                return $"\"{schemaId.GetTypeName(TargetLanguage.Independent)}\"";
            }

            if (dtSchema.EntityKind == DTEntityKind.Object)
            {
                definedIds.Add(dtSchema.Id);
                var templateTransform = new ObjectAvroSchema(schemaId, sharedNamespace, ((DTObjectInfo)dtSchema).Fields.Where(f => !IsFieldErrorCode(f, mqttVersion) && !IsFieldErrorInfo(f, mqttVersion)).Select(f => (f.Name, f.Schema, IsIndirect(f, mqttVersion), IsRequired(f))).ToList(), indent, sharedPrefix, definedIds, mqttVersion);
                return templateTransform.TransformText();
            }

            if (dtSchema.EntityKind == DTEntityKind.Enum)
            {
                definedIds.Add(dtSchema.Id);
                var templateTransform = new EnumAvroSchema(schemaId, sharedNamespace, ((DTEnumInfo)dtSchema).EnumValues.Select(v => v.Name).ToList(), indent);
                return templateTransform.TransformText();
            }

            if (dtSchema.EntityKind == DTEntityKind.Array)
            {
                definedIds.Add(dtSchema.Id);
                var templateTransform = new ArrayAvroSchema(schemaId, ((DTArrayInfo)dtSchema).ElementSchema, indent, sharedPrefix, definedIds, mqttVersion);
                return templateTransform.TransformText();
            }

            if (dtSchema.EntityKind == DTEntityKind.Map)
            {
                definedIds.Add(dtSchema.Id);
                var templateTransform = new MapAvroSchema(schemaId, ((DTMapInfo)dtSchema).MapValue.Schema, indent, sharedPrefix, definedIds, mqttVersion);
                return templateTransform.TransformText();
            }

            string it = new string(' ', indent);

            return dtSchema.Id.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => "\"boolean\"",
                "dtmi:dtdl:instance:Schema:double;2" => "\"double\"",
                "dtmi:dtdl:instance:Schema:float;2" => "\"float\"",
                "dtmi:dtdl:instance:Schema:integer;2" => "\"int\"",
                "dtmi:dtdl:instance:Schema:long;2" => "\"long\"",
                "dtmi:dtdl:instance:Schema:byte;4" => "\"int\"",
                "dtmi:dtdl:instance:Schema:short;4" => "\"int\"",
                "dtmi:dtdl:instance:Schema:unsignedInteger;4" => "\"int\"",
                "dtmi:dtdl:instance:Schema:unsignedLong;4" => "\"long\"",
                "dtmi:dtdl:instance:Schema:unsignedByte;4" => "\"int\"",
                "dtmi:dtdl:instance:Schema:unsignedShort;4" => "\"int\"",
                "dtmi:dtdl:instance:Schema:date;2" => $"{{\r\n{it}  \"type\": \"int\",\r\n{it}  \"logicalType\": \"date\"\r\n{it}}}",
                "dtmi:dtdl:instance:Schema:dateTime;2" => $"{{\r\n{it}  \"type\": \"long\",\r\n{it}  \"logicalType\": \"timestamp-millis\"\r\n{it}}}",
                "dtmi:dtdl:instance:Schema:time;2" => $"{{\r\n{it}  \"type\": \"int\",\r\n{it}  \"logicalType\": \"time-millis\"\r\n{it}}}",
                "dtmi:dtdl:instance:Schema:duration;2" => "\"string\"",
                "dtmi:dtdl:instance:Schema:string;2" => "\"string\"",
                "dtmi:dtdl:instance:Schema:uuid;4" => "\"string\"",
                "dtmi:dtdl:instance:Schema:bytes;4" => "\"bytes\"",
                "dtmi:dtdl:instance:Schema:decimal;4" => "\"string\"",
                _ => string.Empty,
            };
        }

        private static bool IsRequired(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Any(t => DtdlMqttExtensionValues.RequiredAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
        }

        private static bool IsIndirect(DTFieldInfo dtField, int mqttVersion)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IndirectAdjunctTypeFormat, mqttVersion)));
        }

        private static bool IsFieldErrorCode(DTFieldInfo dtField, int mqttVersion)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorCodeAdjunctTypeFormat, mqttVersion)));
        }

        private static bool IsFieldErrorInfo(DTFieldInfo dtField, int mqttVersion)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorInfoAdjunctTypeFormat, mqttVersion)));
        }
    }
}
