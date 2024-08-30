namespace Akri.Dtdl.Codegen
{
    public static class SchemaNames
    {
        public const string AggregateTelemSchema = "TelemetryCollection";

        public static string GetTelemSchema(string telemName) => $"{NameFormatter.Capitalize(telemName)}Telemetry";

        public static string GetCmdReqSchema(string cmdName) => $"{NameFormatter.Capitalize(cmdName)}CommandRequest";

        public static string GetCmdRespSchema(string cmdName) => $"{NameFormatter.Capitalize(cmdName)}CommandResponse";
    }
}
