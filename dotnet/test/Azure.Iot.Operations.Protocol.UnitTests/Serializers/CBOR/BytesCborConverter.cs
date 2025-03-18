// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using Dahomey.Cbor.Serialization;
    using Dahomey.Cbor.Serialization.Converters;

    /// <summary>
    /// Class for customized CBOR conversion of <c>byte[]</c> values to/from Base64 string representations per RFC 4648.
    /// </summary>
    internal sealed class BytesCborConverter : CborConverterBase<byte[]>
    {
        /// <inheritdoc/>
        public override byte[] Read(ref CborReader reader)
        {
            return Convert.FromBase64String(reader.ReadString()!);
        }

        /// <inheritdoc/>
        public override void Write(ref CborWriter writer, byte[] value)
        {
            writer.WriteString(Convert.ToBase64String(value));
        }
    }
}
