/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
{
    using System;
    using System.Collections.Generic;
    using Avro;
    using Avro.Specific;
    using TestEnvoys;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public class MemoryStatsSchemaBase
    {
        public virtual Schema Schema { get => Schema.Parse(@"{""namespace"":""TestEnvoys.Memmon"",""name"":""MemoryStatsSchema"",""type"":""record"",""fields"":[]}"); }
    }

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public partial class MemoryStatsSchema : MemoryStatsSchemaBase, ISpecificRecord
    {
        public double? ManagedMemory { get; set; } = default;

        public double? WorkingSet { get; set; } = default;

        public virtual object Get(int fieldPos)
        {
            switch (fieldPos)
            {
                case 0: return this.ManagedMemory!;
                case 1: return this.WorkingSet!;
                default: throw new Avro.AvroRuntimeException("Bad index " + fieldPos + " in Get()");
            };
        }

        public virtual void Put(int fieldPos, object fieldValue)
        {
            switch (fieldPos)
            {
                case 0: this.ManagedMemory = (double?)fieldValue; break;
                case 1: this.WorkingSet = (double?)fieldValue; break;
                default: throw new Avro.AvroRuntimeException("Bad index " + fieldPos + " in Put()");
            };
        }
    }
}
