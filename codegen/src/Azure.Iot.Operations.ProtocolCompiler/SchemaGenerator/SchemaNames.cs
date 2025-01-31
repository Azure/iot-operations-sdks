namespace Azure.Iot.Operations.ProtocolCompiler
{
    public static class SchemaNames
    {
        public static CodeName AggregateTelemSchema = new CodeName(string.Empty, "telemetry", "collection");

        public static CodeName GetTelemSchema(string telemName) => new CodeName(telemName, "telemetry");

        public static CodeName GetCmdReqSchema(string cmdName) => new CodeName(cmdName, "request", "payload");

        public static CodeName GetCmdRespSchema(string cmdName) => new CodeName(cmdName, "response", "payload");
    }
}
