// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using System.Globalization;
    using Dahomey.Cbor.Serialization;
    using Dahomey.Cbor.Serialization.Converters;

    /// <summary>
    /// Class for customized CBOR conversion of <c>DateOnly</c> values to/from string representations in ISO 8601 Date format.
    /// </summary>
    internal sealed class DateCborConverter : CborConverterBase<DateOnly>
    {
        /// <inheritdoc/>
        public override DateOnly Read(ref CborReader reader)
        {
            return DateOnly.Parse(reader.ReadString()!, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public override void Write(ref CborWriter writer, DateOnly value)
        {
            writer.WriteString(value.ToString("o", CultureInfo.InvariantCulture));
        }
    }
}
