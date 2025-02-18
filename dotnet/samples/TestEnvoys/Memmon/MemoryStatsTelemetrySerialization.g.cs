/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using Avro;

    public partial class MemoryStatsTelemetry
    {
        private const string RAW_SCHEMA = @"
{
  ""namespace"": ""TestEnvoys.Memmon"",
  ""name"": ""MemoryStatsTelemetry"",
  ""type"": ""record"",
  ""fields"": [
    {
      ""name"": ""memoryStats"",
      ""type"": {
""name"": ""MemoryStatsSchema"",
""type"": ""record"",
""fields"": [
  {
    ""name"": ""managedMemory"",
""type"": [
  ""null"",
  {
    ""type"": ""double""
  }
]
  },
  {
    ""name"": ""workingSet"",
""type"": [
  ""null"",
  {
    ""type"": ""double""
  }
]
  }
]
      }
    }
  ]
}
";

        public override Schema Schema { get => Schema.Parse(RAW_SCHEMA); }
    }
}
