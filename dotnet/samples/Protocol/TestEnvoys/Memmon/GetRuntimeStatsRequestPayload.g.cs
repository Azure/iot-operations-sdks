/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using System;
    using System.Collections.Generic;
    using Avro;
    using Avro.Specific;
    using TestEnvoys;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public class GetRuntimeStatsRequestPayloadBase
    {
        public virtual Schema Schema { get => Schema.Parse(@"{""namespace"":""TestEnvoys.Memmon"",""name"":""GetRuntimeStatsRequestPayload"",""type"":""record"",""fields"":[]}"); }
    }

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class GetRuntimeStatsRequestPayload : GetRuntimeStatsRequestPayloadBase, ISpecificRecord
    {
        public requiredGetRuntimeStatsRequestSchema DiagnosticsMode { get; set; } 

        public virtual object Get(int fieldPos)
        {
            switch (fieldPos)
            {
                case 0: return this.DiagnosticsMode!;
                default: throw new Avro.AvroRuntimeException("Bad index " + fieldPos + " in Get()");
            };
        }

        public virtual void Put(int fieldPos, object fieldValue)
        {
            switch (fieldPos)
            {
                case 0: this.DiagnosticsMode = (GetRuntimeStatsRequestSchema)(int)fieldValue; break;
                default: throw new Avro.AvroRuntimeException("Bad index " + fieldPos + " in Put()");
            };
        }
    }
}
