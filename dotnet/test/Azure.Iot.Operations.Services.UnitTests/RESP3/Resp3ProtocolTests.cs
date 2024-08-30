using System.Text;
using Azure.Iot.Operations.Services.StateStore.RESP3;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore.RESP3
{
    public class Resp3ProtocolTests
    {
        [Fact]
        public void ParseBlobStringSuccess()
        {
            // arrange
            string value = "$11\r\nhello world\r\n";

            // act
            byte[] parsedValue = Resp3Protocol.ParseBlobString(Encoding.ASCII.GetBytes(value)).ToArray();

            // assert
            Assert.Equal("hello world", Encoding.ASCII.GetString(parsedValue));
        }

        [Fact]
        public void ParseBlobStringWithControlledIndexSuccess()
        {
            // arrange
            string value = "$11\r\nhello world\r\n";

            // act
            int remainingIndex = Resp3Protocol.ParseBlobString(0, Encoding.ASCII.GetBytes(value), out ReadOnlySpan<byte> output);

            // assert
            Assert.Equal("hello world", Encoding.ASCII.GetString(output.ToArray()));
            Assert.Equal(value.Length, remainingIndex);
        }

        [Fact]
        public void ParseBlobStringWithOffsetStartingIndexSuccess()
        {
            // arrange
            string value = "___$11\r\nhello world\r\n";

            // act
            int remainingIndex = Resp3Protocol.ParseBlobString(3, Encoding.ASCII.GetBytes(value), out ReadOnlySpan<byte> output);

            // assert
            Assert.Equal("hello world", Encoding.ASCII.GetString(output.ToArray()));
            Assert.Equal(value.Length, remainingIndex);
        }

        [Fact]
        public void ParseBlobStringWithTrailingCharactersSuccess()
        {
            // arrange
            string value = "$11\r\nhello world\r\n+++";

            // act
            int remainingIndex = Resp3Protocol.ParseBlobString(0, Encoding.ASCII.GetBytes(value), out ReadOnlySpan<byte> output);

            // assert
            Assert.Equal("hello world", Encoding.ASCII.GetString(output.ToArray()));
            Assert.Equal(value.Length - 3, remainingIndex);
        }

        [Fact]
        public void ParseBlobStringWithOffsetStartingIndexAndTrailingCharactersSuccess()
        {
            // arrange
            string value = "___$11\r\nhello world\r\n+++";

            // act
            int remainingIndex = Resp3Protocol.ParseBlobString(3, Encoding.ASCII.GetBytes(value), out ReadOnlySpan<byte> output);

            // assert
            Assert.Equal("hello world", Encoding.ASCII.GetString(output.ToArray()));
            Assert.Equal(value.Length - 3, remainingIndex);
        }

        [Fact]
        public void ParseBlobStringThrowsIfArgumentTooShort()
        {
            // arrange
            byte[] value = new byte[2];

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobString(value));
        }

        [Fact]
        public void ParseBlobStringThrowsIfArgumentDoesNotStartWithDollarSign()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("11\r\nhello world\r\n");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobString(value));
        }

        [Fact]
        public void ParseBlobStringThrowsIfArgumentDoesNotHaveFirstNewline()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("$11hello world\r\n");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobString(value));
        }

        [Fact]
        public void ParseBlobStringThrowsIfArgumentDoesNotHaveSecondNewline()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("$11\r\nhello world");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobString(value));
        }

        [Fact]
        public void ParseBlobStringThrowsIfArgumentLengthIsNotInteger()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("$not an integer\r\nhello world");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobString(value));
        }

        [Fact]
        public void ParseBlobStringThrowsIfArgumentLengthIsNotAccurate()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("$11\r\nthis string is longer than 11 characters\r\n");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobString(value));
        }

        [Fact]
        public void ParseBlobStringAllowsEmpty()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("$0\r\n\r\n");

            // act
            ReadOnlySpan<byte> actual = Resp3Protocol.ParseBlobString(value);

            // assert
            Assert.Equal(0, actual.Length);
        }

        [Fact]
        public void ThrowIfSimpleErrorSuccess()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("-ERR this is the error description\r\n");

            // act
            Resp3SimpleErrorException thrownException = 
                Assert.Throws<Resp3SimpleErrorException>(() => Resp3Protocol.ThrowIfSimpleError(value));

            // assert
            Assert.Equal("this is the error description", thrownException.ErrorDescription);
        }

        [Fact]
        public void ThrowIfSimpleErrorSuccessEmptyDescription()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("-ERR \r\n");

            // act
            Resp3SimpleErrorException thrownException =
                Assert.Throws<Resp3SimpleErrorException>(() => Resp3Protocol.ThrowIfSimpleError(value));

            // assert
            Assert.Equal("", thrownException.ErrorDescription);
        }

        [Fact]
        public void ThrowIfSimpleErrorWithoutDescriptionDoesNotThrow()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("-ERR\r\n");

            // act/assert
            try
            {
                Resp3Protocol.ThrowIfSimpleError(value);
            }
            catch (Resp3SimpleErrorException)
            {
                // all simple errors must have a description and this payload doesn't
                Assert.Fail("Expected no exception to be thrown");
            }
        }

        [Fact]
        public void ParseSimpleErrorThrowsIfItIsTooShortToParse()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("-");

            // act, assert
            try
            { 
                Resp3Protocol.ThrowIfSimpleError(value);
            }
            catch (Resp3SimpleErrorException)
            {
                Assert.Fail("Expected no exception to be thrown");
            }
        }

        [Fact]
        public void ThrowIfSimpleErrorDoesNotThrowIfItDoesNotStartWithMinusSign()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("ERR description\r\n");

            // act, assert
            try
            {
                Resp3Protocol.ThrowIfSimpleError(value);
            }
            catch (Resp3SimpleErrorException)
            {
                Assert.Fail("Expected no exception to be thrown");
            }
        }

        [Fact]
        public void ThrowIfSimpleErrorDoesNotThrowIfItDoesNotEndWithNewLine()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("-ERR description");

            // act, assert
            try
            {
                Resp3Protocol.ThrowIfSimpleError(value);
            }
            catch (Resp3SimpleErrorException)
            {
                Assert.Fail("Expected no exception to be thrown");
            }
        }

        [Fact]
        public void ParseNumberSuccess()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes(":1234\r\n");

            // act
            int parsedValue = Resp3Protocol.ParseNumber(value);

            // assert
            Assert.Equal(1234, parsedValue);
        }

        [Fact]
        public void ParseNumberThrowsIfTooShortToParse()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes(":");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseNumber(value));
        }

        [Fact]
        public void ParseNumberThrowsIfItDoesNotStartWithColon()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("1234\r\n");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseNumber(value));
        }

        [Fact]
        public void ParseNumberThrowsIfItDoesNotEndWithNewLine()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes(":1234");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseNumber(value));
        }

        [Fact]
        public void ParseNumberThrowsIfValueIsNotInteger()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes(":not an integer\r\n");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseNumber(value));
        }

        [Fact]
        public void ParseSimpleStringSuccess()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("+hello world\r\n");

            // act
            string parsedValue = Resp3Protocol.ParseSimpleString(value);

            // assert
            Assert.Equal("hello world", parsedValue);
        }

        [Fact]
        public void ParseSimpleStringThrowsIfTooShortToParse()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("+");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseSimpleString(value));
        }

        [Fact]
        public void ParseSimpleStringThrowsIfItDoesNotStartWithPlusSign()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("hello world\r\n");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseSimpleString(value));
        }

        [Fact]
        public void ParseSimpleStringThrowsIfItDoesNotEndWithNewLine()
        {
            // arrange
            byte[] value = Encoding.ASCII.GetBytes("+hello world");

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseSimpleString(value));
        }

        [Fact]
        public void BuildArraySuccess()
        {
            // arrange
            byte[] expected = Encoding.ASCII.GetBytes("*3\r\n$3\r\nset\r\n$3\r\nkey\r\n$5\r\nvalue\r\n");
            byte[] value1 = Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("set"));
            byte[] value2 = Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("key"));
            byte[] value3 = Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("value"));

            // act
            byte[] actual = Resp3Protocol.BuildArray(value1, value2, value3);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildArrayThrowsIfNoElements()
        {
            // arrange
            byte[] expected = Encoding.ASCII.GetBytes("*3\r\n$3\r\nset\r\n$3\r\nkey\r\n$5\r\nvalue\r\n");

            // act, assert
            Assert.Throws<ArgumentException>(() => Resp3Protocol.BuildArray());
        }

        [Fact]
        public void BuildBlobStringSuccess()
        {
            // arrange
            byte[] expected = Encoding.ASCII.GetBytes("$3\r\nset\r\n");

            // act
            byte[] actual = Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("set"));

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildBlobStringThrowsIfNull()
        {
            // arrange, act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.BuildBlobString(null!));
        }

        [Fact]
        public void BuildBlobStringAllowsEmpty()
        {
            // arrange
            byte[] expected = Encoding.ASCII.GetBytes("$0\r\n\r\n");

            // act
            byte[] actual = Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes(""));

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ParseBlobArraySuccess()
        {
            // arrange
            string value = "*2\r\n$11\r\nhello world\r\n$7\r\ngoodbye\r\n";

            // act
            List<byte[]> parsedValue = Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value));

            // assert
            Assert.Equal(2, parsedValue.Count);
            Assert.Equal("hello world", Encoding.ASCII.GetString(parsedValue[0]));
            Assert.Equal("goodbye", Encoding.ASCII.GetString(parsedValue[1]));
        }

        [Fact]
        public void ParseBlobArraySuccessZeroLength()
        {
            // arrange
            string value = "*0\r\n";

            // act
            List<byte[]> parsedValue = Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value));

            // assert
            Assert.Empty(parsedValue);
        }

        [Fact]
        public void ParseBlobArrayThrowsIfArrayLengthDoesNotMatchActualLength()
        {
            // arrange
            string value = "*2\r\n$11\r\nhello world\r\n";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }

        [Fact]
        public void ParseBlobArrayThrowsIfExtraCharactersArePresentAtEnd()
        {
            // arrange
            string value = "*2\r\n$11\r\nhello world\r\n$7\r\ngoodbye\r\nthisShouldNotBeHere";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }

        [Fact]
        public void ParseBlobArrayThrowsIfMissingStar()
        {
            // arrange
            string value = "2\r\n$11\r\nhello world\r\n$7\r\ngoodbye\r\n";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }

        [Fact]
        public void ParseBlobArrayThrowsIfMissingArrayLength()
        {
            // arrange
            string value = "*\r\n$11\r\nhello world\r\n$7\r\ngoodbye\r\n";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }

        [Fact]
        public void ParseBlobArrayThrowsIfArrayLengthIsNotInteger()
        {
            // arrange
            string value = "*thisShouldNotBeHere\r\n$11\r\nhello world\r\n$7\r\ngoodbye\r\n";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }

        [Fact]
        public void ParseBlobArrayThrowsIfArrayEntryIsNotABlobString()
        {
            // arrange
            string value = "*2\r\n$11\r\nhello world\r\n:1234\r\n";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }

        [Fact]
        public void ParseBlobArrayThrowsIfMissingFirstSeperator()
        {
            // arrange
            string value = "*2$11\r\nhello world\r\n$7\r\ngoodbye\r\n";

            // act, assert
            Assert.Throws<Resp3ProtocolException>(() => Resp3Protocol.ParseBlobArray(Encoding.ASCII.GetBytes(value)));
        }
    }
}
