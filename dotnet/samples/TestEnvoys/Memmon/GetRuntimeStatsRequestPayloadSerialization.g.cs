/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using Avro;

    public partial class GetRuntimeStatsRequestPayload
    {
        private const string RAW_SCHEMA = @"
{
  ""namespace"": ""TestEnvoys.Memmon"",
  ""name"": ""GetRuntimeStatsRequestPayload"",
  ""type"": ""record"",
  ""fields"": [
    {
      ""name"": ""diagnosticsMode"",
      ""type"": {
""name"": ""GetRuntimeStatsRequestSchema"",
""type"": ""enum"",
""symbols"": [ ""minimal"", ""complete"", ""full"" ]
      }
    }
  ]
}
";

        public override Schema Schema { get => Schema.Parse(RAW_SCHEMA); }
    }
}
