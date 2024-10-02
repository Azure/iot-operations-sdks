namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public static class ProtobufSupport
    {
        private static readonly HashSet<string> ScalarTypes = new()
        {
            "dtmi:dtdl:instance:Schema:boolean;2",
            "dtmi:dtdl:instance:Schema:double;2",
            "dtmi:dtdl:instance:Schema:float;2",
            "dtmi:dtdl:instance:Schema:integer;2",
            "dtmi:dtdl:instance:Schema:long;2",
            "dtmi:dtdl:instance:Schema:byte;4",
            "dtmi:dtdl:instance:Schema:short;4",
            "dtmi:dtdl:instance:Schema:unsignedInteger;4",
            "dtmi:dtdl:instance:Schema:unsignedLong;4",
            "dtmi:dtdl:instance:Schema:unsignedByte;4",
            "dtmi:dtdl:instance:Schema:unsignedShort;4",
        };

        public static string GetType(DTSchemaInfo dtSchema, DtmiToSchemaName dtmiToSchemaName)
        {
            return dtSchema.Id.AbsoluteUri switch
            {
                "dtmi:dtdl:instance:Schema:boolean;2" => "bool",
                "dtmi:dtdl:instance:Schema:double;2" => "double",
                "dtmi:dtdl:instance:Schema:float;2" => "float",
                "dtmi:dtdl:instance:Schema:integer;2" => "sint32",
                "dtmi:dtdl:instance:Schema:long;2" => "sint64",
                "dtmi:dtdl:instance:Schema:byte;4" => "sint32",
                "dtmi:dtdl:instance:Schema:short;4" => "sint32",
                "dtmi:dtdl:instance:Schema:unsignedInteger;4" => "uint32",
                "dtmi:dtdl:instance:Schema:unsignedLong;4" => "uint64",
                "dtmi:dtdl:instance:Schema:unsignedByte;4" => "uint32",
                "dtmi:dtdl:instance:Schema:unsignedShort;4" => "uint32",
                "dtmi:dtdl:instance:Schema:date;2" => "google.protobuf.Timestamp", // google.type.Date not supported in Java
                "dtmi:dtdl:instance:Schema:dateTime;2" => "google.protobuf.Timestamp",
                "dtmi:dtdl:instance:Schema:time;2" => "google.protobuf.Timestamp", // google.type.TimeOfDay not supported in Java
                "dtmi:dtdl:instance:Schema:duration;2" => "google.protobuf.Duration",
                "dtmi:dtdl:instance:Schema:string;2" => "string",
                _ => dtmiToSchemaName(dtSchema.Id, dtSchema.EntityKind.ToString()),
            };
        }

        public static bool IsScalar(DTSchemaInfo dtSchema)
        {
            return ScalarTypes.Contains(dtSchema.Id.AbsoluteUri);
        }

        public static bool TryGetImport(DTSchemaInfo dtSchema, DtmiToSchemaName dtmiToSchemaName, out string importName)
        {
            if (dtSchema.EntityKind == DTEntityKind.Array || dtSchema.EntityKind == DTEntityKind.Map || dtSchema.EntityKind == DTEntityKind.Object || dtSchema.EntityKind == DTEntityKind.Enum)
            {
                string schemaName = dtmiToSchemaName(dtSchema.Id, dtSchema.EntityKind.ToString());
                importName = $"{schemaName}.proto";
                return true;
            }
            else if (dtSchema.Id.AbsoluteUri == "dtmi:dtdl:instance:Schema:date;2")
            {
                importName = "google/type/date.proto"; // supported by C# but not by Java
                importName = "google/protobuf/timestamp.proto";
                return true;
            }
            else if (dtSchema.Id.AbsoluteUri == "dtmi:dtdl:instance:Schema:dateTime;2")
            {
                importName = "google/protobuf/timestamp.proto";
                return true;
            }
            else if (dtSchema.Id.AbsoluteUri == "dtmi:dtdl:instance:Schema:time;2")
            {
                importName = "google/type/timeofday.proto"; // supported by C# but not by Java
                importName = "google/protobuf/timestamp.proto";
                return true;
            }
            else if (dtSchema.Id.AbsoluteUri == "dtmi:dtdl:instance:Schema:duration;2")
            {
                importName = "google/protobuf/duration.proto";
                return true;
            }
            else
            {
                importName = string.Empty;
                return false;
            }
        }
    }
}
