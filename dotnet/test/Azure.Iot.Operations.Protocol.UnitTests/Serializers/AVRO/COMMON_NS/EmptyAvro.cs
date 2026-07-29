// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

using Avro;
using Avro.Specific;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.AVRO
{
    public class EmptyAvro : ISpecificRecord
    {
        public Schema Schema
        {
            get => PrimitiveSchema.Create(Schema.Type.Null);
        }

        public object Get(int fieldPos)
        {
            return null!;
        }

        public void Put(int fieldPos, object fieldValue)
        {
        }
    }
}
