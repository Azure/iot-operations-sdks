﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.common
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    public class DecimalString
    {
        private static readonly Regex validationRegex = new Regex("^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$", RegexOptions.Compiled);

        private readonly string _value;

        public static bool TryParse(string value, out DecimalString? decimalString)
        {
            if (validationRegex.IsMatch(value))
            {
                decimalString = new DecimalString(value, skipValidation: true);
                return true;
            }
            else
            {
                decimalString = null;
                return false;
            }
        }

        public DecimalString()
            : this("0", skipValidation: false)
        {
        }

        public DecimalString(string value)
            : this(value, skipValidation: false)
        {
        }

        public static implicit operator string(DecimalString decimalString) => decimalString._value;
        public static explicit operator DecimalString(string stringVal) => new DecimalString(stringVal);

        public static implicit operator double(DecimalString decimalString) => double.TryParse(decimalString._value, out double doubleVal) ? doubleVal : double.NaN;
        public static explicit operator DecimalString(double doubleVal) => new DecimalString(doubleVal.ToString("F", CultureInfo.InvariantCulture));

        public static bool operator !=(DecimalString? x, DecimalString? y)
        {
            if (ReferenceEquals(null, x))
            {
                return !ReferenceEquals(null, y);
            }

            return !x.Equals(y);
        }

        public static bool operator ==(DecimalString? x, DecimalString? y)
        {
            if (ReferenceEquals(null, x))
            {
                return ReferenceEquals(null, y);
            }

            return x.Equals(y);
        }

        public virtual bool Equals(DecimalString? other)
        {
            return other?._value == this?._value;
        }

        public override bool Equals(object? obj)
        {
            return obj is DecimalString other && this.Equals(other);
        }
        public override int GetHashCode()
        {
            return this._value.GetHashCode();
        }

        private DecimalString(string value, bool skipValidation)
        {
            if (!skipValidation && !validationRegex.IsMatch(value))
            {
                throw new ArgumentException($"string {value} is not a valid decimal value");
            }

            this._value = value;
        }

        public override string ToString() => _value;
    }
}
