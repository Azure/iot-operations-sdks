/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using System.Xml;
    using Dahomey.Cbor.Serialization;
    using Dahomey.Cbor.Serialization.Converters;

    /// <summary>
    /// Class for customized CBOR conversion of <c>TimeSpan</c> values to/from string representations in ISO 8601 Duration format.
    /// </summary>
    internal sealed class DurationCborConverter : CborConverterBase<TimeSpan>
    {
        /// <inheritdoc/>
        public override TimeSpan Read(ref CborReader reader)
        {
            return XmlConvert.ToTimeSpan(reader.ReadString()!);
        }

        /// <inheritdoc/>
        public override void Write(ref CborWriter writer, TimeSpan value)
        {
            writer.WriteString(XmlConvert.ToString(value));
        }
    }
}
