// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Serializers.common;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class DecimalStringUnitTests
    {
        public static IEnumerable<object?[]> GetValidDecimalStringValues()
        {
            yield return new object?[] { "0", 0.0 };
            yield return new object?[] { "0.", 0.0 };
            yield return new object?[] { "0.0", 0.0 };
            yield return new object?[] { "-0", 0.0 };
            yield return new object?[] { "+0", 0.0 };
            yield return new object?[] { "1", 1.0 };
            yield return new object?[] { "1.", 1.0 };
            yield return new object?[] { "1.0", 1.0 };
            yield return new object?[] { "22.2", 22.2 };
            yield return new object?[] { "-22.2", -22.2 };
            yield return new object?[] { "+22.2", 22.2 };
        }

        public static IEnumerable<object?[]> GetInvalidDecimalStringValues()
        {
            yield return new object?[] { "00" };
            yield return new object?[] { "01" };
            yield return new object?[] { "++1" };
            yield return new object?[] { ".5" };
            yield return new object?[] { "1.2.3" };
            yield return new object?[] { "1..23" };
            yield return new object?[] { "one" };
        }

        public static IEnumerable<object?[]> GetNormalizedValidDecimalStringValues()
        {
            yield return new object?[] { "0.00", 0.0 };
            yield return new object?[] { "1.00", 1.0 };
            yield return new object?[] { "22.20", 22.2 };
            yield return new object?[] { "-22.20", -22.2 };
        }

        public static IEnumerable<object?[]> GetDecimalStringPairs()
        {
            yield return new object?[] { null, null, true };
            yield return new object?[] { new DecimalString("0"), new DecimalString("0"), true };
            yield return new object?[] { new DecimalString("0.0"), new DecimalString("0.0"), true };
            yield return new object?[] { new DecimalString("0"), new DecimalString("0.0"), false };
            yield return new object?[] { new DecimalString("-0"), new DecimalString("0"), false };
            yield return new object?[] { new DecimalString("+0"), new DecimalString("0"), false };
            yield return new object?[] { new DecimalString("1"), new DecimalString("1"), true };
            yield return new object?[] { new DecimalString("1.000"), new DecimalString("1.000"), true };
            yield return new object?[] { new DecimalString("1.000"), new DecimalString("1.00"), false };
        }

        [Theory]
        [MemberData(nameof(GetValidDecimalStringValues))]
        public void TryParseReturnsTrueOnValidStrings(string stringValue, double _)
        {
            DecimalString? decimalString;
            Assert.True(DecimalString.TryParse(stringValue, out decimalString));
            Assert.NotNull(decimalString);
            Assert.Equal(stringValue, decimalString.ToString());
        }

        [Theory]
        [MemberData(nameof(GetInvalidDecimalStringValues))]
        public void TryParseReturnsFalseOnInvalidStrings(string stringValue)
        {
            DecimalString? decimalString = new DecimalString("1");
            Assert.False(DecimalString.TryParse(stringValue, out decimalString));
            Assert.Null(decimalString);
        }

        [Theory]
        [MemberData(nameof(GetValidDecimalStringValues))]
        public void ConstructorSucceedsOnValidStrings(string stringValue, double _)
        {
            DecimalString? decimalString = new DecimalString(stringValue);
            Assert.Equal(stringValue, decimalString.ToString());
        }

        [Theory]
        [MemberData(nameof(GetInvalidDecimalStringValues))]
        public void ConstructorThrowsOnInvalidStrings(string stringValue)
        {
            Assert.Throws<ArgumentException>(() => { new DecimalString(stringValue); });
        }

        [Theory]
        [MemberData(nameof(GetValidDecimalStringValues))]
        public void CanImplicitlyConvertToString(string stringValue, double _)
        {
            DecimalString? decimalString = new DecimalString(stringValue);
            string value = decimalString;
            Assert.Equal(stringValue, value);
        }

        [Theory]
        [MemberData(nameof(GetValidDecimalStringValues))]
        public void CanExplicitlyConverFromString(string stringValue, double _)
        {
            DecimalString? decimalString = (DecimalString)stringValue;
            Assert.Equal(stringValue, decimalString);
        }

        [Theory]
        [MemberData(nameof(GetValidDecimalStringValues))]
        public void CanImplicitlyConvertToDouble(string stringValue, double doubleValue)
        {
            DecimalString? decimalString = new DecimalString(stringValue);
            double value = decimalString;
            Assert.Equal(doubleValue, value);
        }

        [Theory]
        [MemberData(nameof(GetNormalizedValidDecimalStringValues))]
        public void CanExplicitlyConverFromDouble(string stringValue, double doubleValue)
        {
            DecimalString? decimalString = (DecimalString)doubleValue;
            Assert.Equal(stringValue, decimalString);
        }

        [Theory]
        [MemberData(nameof(GetDecimalStringPairs))]
        public void EqualsOperatorChecksEquality(DecimalString? decimalString1, DecimalString? decimalString2, bool areEqual)
        {
            Assert.Equal(areEqual, decimalString1 == decimalString2);
            Assert.Equal(areEqual, decimalString2 == decimalString1);
        }

        [Theory]
        [MemberData(nameof(GetDecimalStringPairs))]
        public void NotEqualsOperatorChecksInequality(DecimalString? decimalString1, DecimalString? decimalString2, bool areEqual)
        {
            Assert.Equal(!areEqual, decimalString1 != decimalString2);
            Assert.Equal(!areEqual, decimalString2 != decimalString1);
        }

        [Theory]
        [MemberData(nameof(GetDecimalStringPairs))]
        public void EqualsMethodChecksEquality(DecimalString? decimalString1, DecimalString? decimalString2, bool areEqual)
        {
            if (decimalString1 != null)
            {
                Assert.Equal(areEqual, decimalString1.Equals(decimalString2));
            }

            if (decimalString2 != null)
            {
                Assert.Equal(areEqual, decimalString2.Equals(decimalString1));
            }
        }

        [Fact]
        public void EqualsObjectMethodChecksEquality()
        {
            DecimalString decimalString1 = new DecimalString("1");
            Assert.False(decimalString1.Equals(null));
            Assert.False(decimalString1.Equals((DecimalString?)null));
            Assert.False(decimalString1.Equals(1));
            Assert.False(decimalString1.Equals("1"));
            Assert.False(decimalString1.Equals("one"));
            Assert.True(decimalString1.Equals((object)new DecimalString("1")));
        }
    }
}
