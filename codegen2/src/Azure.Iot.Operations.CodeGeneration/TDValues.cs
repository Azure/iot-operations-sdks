namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;

    public static class TDValues
    {
        public const string RelationSchemaNaming = "service-desc";

        public const string ContentTypeJson = "application/json";

        public const string OpInvokeAction = "invokeaction";
        public const string OpQueryAction = "queryaction";
        public const string OpReadProp = "readproperty";
        public const string OpWriteProp = "writeproperty";
        public const string OpSubEvent = "subscribeevent";
        public const string OpReadAllProps = "readallproperties";
        public const string OpWriteMultProps = "writemultipleproperties";
        public const string OpSubAllEvents = "subscribeallevents";

        public const string TypeObject = "object";
        public const string TypeArray = "array";
        public const string TypeString = "string";
        public const string TypeNumber = "number";
        public const string TypeInteger = "integer";
        public const string TypeBoolean = "boolean";
        public const string TypeNull = "null";

        public const string FormatDateTime = "date-time";
        public const string FormatDate = "date";
        public const string FormatTime = "time";
        public const string FormatUuid = "uuid";

        public const string ContentEncodingBase64 = "base64";

        public static readonly HashSet<string> OpValues = new HashSet<string>() {
            OpReadProp,
            OpWriteProp,
            OpReadAllProps,
            OpWriteMultProps,
            OpSubAllEvents,
        };

        public static readonly HashSet<string> TypeValues = new HashSet<string>() {
            TypeObject,
            TypeArray,
            TypeString,
            TypeNumber,
            TypeInteger,
            TypeBoolean,
            TypeNull,
        };

        public static readonly HashSet<string> FormatValues = new HashSet<string>() {
            FormatDateTime,
            FormatDate,
            FormatTime,
            FormatUuid,
        };
    }
}
