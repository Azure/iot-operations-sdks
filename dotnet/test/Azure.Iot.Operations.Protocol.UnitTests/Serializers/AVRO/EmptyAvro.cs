// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.AVRO
{
    using Avro;
    using Avro.Specific;

    public class EmptyAvro : ISpecificRecord
    {
        public Schema Schema { get => PrimitiveSchema.Create(Schema.Type.Null); }
        public object Get(int fieldPos) { return null!; }
        public void Put(int fieldPos, object fieldValue) { }
    }
}
