namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using System.Globalization;
    using Dahomey.Cbor.Serialization;
    using Dahomey.Cbor.Serialization.Converters;

    /// <summary>
    /// Class for customized CBOR conversion of <c>TimeOnly</c> values to/from string representations in ISO 8601 Time format.
    /// </summary>
    internal sealed class TimeCborConverter : CborConverterBase<TimeOnly>
    {
        /// <inheritdoc/>
        public override TimeOnly Read(ref CborReader reader)
        {
            return TimeOnly.Parse(reader.ReadString()!, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public override void Write(ref CborWriter writer, TimeOnly value)
        {
            writer.WriteString(value.ToString("o", CultureInfo.InvariantCulture));
        }
    }
}
