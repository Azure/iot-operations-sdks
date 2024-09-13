/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using Dahomey.Cbor.Serialization;
    using Dahomey.Cbor.Serialization.Converters;

    /// <summary>
    /// Class for customized CBOR conversion of <c>Guid</c> values to/from UUID string representations per RFC 9562.
    /// </summary>
    internal sealed class UuidCborConverter : CborConverterBase<Guid>
    {
        /// <inheritdoc/>
        public override Guid Read(ref CborReader reader)
        {
            return Guid.ParseExact(reader.ReadString()!, "D");
        }

        /// <inheritdoc/>
        public override void Write(ref CborWriter writer, Guid value)
        {
            writer.WriteString(value.ToString("D"));
        }
    }
}
