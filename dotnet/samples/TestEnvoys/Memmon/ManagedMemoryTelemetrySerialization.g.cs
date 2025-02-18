/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using Avro;

    public partial class ManagedMemoryTelemetry
    {
        private const string RAW_SCHEMA = @"
{
  ""namespace"": ""TestEnvoys.Memmon"",
  ""name"": ""ManagedMemoryTelemetry"",
  ""type"": ""record"",
  ""fields"": [
    {
      ""name"": ""managedMemory"",
      ""type"": ""double""
    }
  ]
}
";

        public override Schema Schema { get => Schema.Parse(RAW_SCHEMA); }
    }
}
