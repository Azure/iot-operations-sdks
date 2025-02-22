/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using Avro;

    public partial class GetRuntimeStatsResponsePayload
    {
        private const string RAW_SCHEMA = @"
{
  ""namespace"": ""TestEnvoys.Memmon"",
  ""name"": ""GetRuntimeStatsResponsePayload"",
  ""type"": ""record"",
  ""fields"": [
    {
      ""name"": ""diagnosticResults"",
      ""type"": {
""type"": ""map"",
""values"": {
  ""type"": ""string""
},
""default"": {}
      }
    }
  ]
}
";

        public override Schema Schema { get => Schema.Parse(RAW_SCHEMA); }
    }
}
