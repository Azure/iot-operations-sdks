namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public static class SchemaNames
    {
        public static CodeName AggregateTelemSchema = new CodeName(string.Empty, "telemetry", "collection");

        public static CodeName AggregatePropName = new CodeName(string.Empty, "properties");

        public static CodeName AggregatePropSchema = new CodeName(string.Empty, "property", "collection");

        public static CodeName AggregatePropWriteSchema = new CodeName(string.Empty, "property", "update");

        public static CodeName AggregatePropReadRespSchema = new CodeName(AggregatePropSchema, "read", "response", "schema");

        public static CodeName AggregatePropWriteRespSchema = new CodeName(AggregatePropSchema, "write", "response", "schema");

        public static CodeName AggregatePropReadErrSchema = new CodeName(AggregatePropSchema, "read", "error");

        public static CodeName AggregatePropWriteErrSchema = new CodeName(AggregatePropSchema, "write", "error");

        public static CodeName AggregateReadRespValueField = new CodeName(string.Empty, "properties");

        public static CodeName AggregateRespErrorField = new CodeName(string.Empty, "errors");

        public static CodeName GetTelemSchema(string telemName) => new CodeName(telemName, "telemetry");

        public static CodeName GetPropSchema(string propName) => new CodeName(propName, "property");

        public static CodeName GetWritablePropSchema(string propName) => new CodeName(propName, "writable", "property");

        public static CodeName GetPropReadRespSchema(string propName) => new CodeName(propName, "read", "response", "schema");

        public static CodeName GetPropWriteRespSchema(string propName) => new CodeName(propName, "write", "response", "schema");

        public static CodeName GetCmdReqSchema(string cmdName) => new CodeName(cmdName, "request", "payload");

        public static CodeName GetCmdRespSchema(string cmdName) => new CodeName(cmdName, "response", "payload");
    }
}
