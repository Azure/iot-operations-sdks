/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using Avro;

    public partial class StartTelemetryRequestPayload
    {
        private const string RAW_SCHEMA = @"
{
  ""namespace"": ""TestEnvoys.Memmon"",
  ""name"": ""StartTelemetryRequestPayload"",
  ""type"": ""record"",
  ""fields"": [
    {
      ""name"": ""interval"",
      ""type"": ""int""
    }
  ]
}
";

        public override Schema Schema { get => Schema.Parse(RAW_SCHEMA); }
    }
}
