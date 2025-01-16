/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.dtmi_akri_samples_memmon__1
{
    using System;
    using System.Collections.Generic;
    using Avro;
    using Avro.Specific;
    using TestEnvoys;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public class ManagedMemoryTelemetryBase
    {
        public virtual Schema Schema { get => Schema.Parse(@"{""namespace"":""TestEnvoys.dtmi_akri_samples_memmon__1"",""name"":""ManagedMemoryTelemetry"",""type"":""record"",""fields"":[]}"); }
    }

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public partial class ManagedMemoryTelemetry : ManagedMemoryTelemetryBase, ISpecificRecord
    {
        public double ManagedMemory { get; set; } = default!;

        public virtual object Get(int fieldPos)
        {
            switch (fieldPos)
            {
                case 0: return this.ManagedMemory!;
                default: throw new Avro.AvroRuntimeException("Bad index " + fieldPos + " in Get()");
            };
        }

        public virtual void Put(int fieldPos, object fieldValue)
        {
            switch (fieldPos)
            {
                case 0: this.ManagedMemory = (double)fieldValue; break;
                default: throw new Avro.AvroRuntimeException("Bad index " + fieldPos + " in Put()");
            };
        }
    }
}
