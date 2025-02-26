// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Azure.Iot.Operations.Services.StateStore.RESP3
{
    internal class Resp3Protocol
    {
        // The commonly used separator that splits ups segments of a RESP3 string. AKA "CRLF"
        private const string Separator = "\r\n";

        private static readonly byte[] Nil1 = Encoding.ASCII.GetBytes("$-1\r\n"); // returned when getting a key that does not exist
        private static readonly byte[] Nil2 = Encoding.ASCII.GetBytes(":-1\r\n"); // returned when non-fencing condition isn't met on a set request

        private static readonly byte[] Error = Encoding.ASCII.GetBytes("-ERR");

        /// <summary>
        /// Parse a RESP3 blob string formatted byte[] and return the embedded content byte[]
        /// basic form: "${length}\r\n{bytes}\r\n"
        /// example: "$11\r\nhello world\r\n"
        /// </summary>
        /// <remarks>
        /// Note that "\r\n" may appear within the {bytes} segment, so this function
        /// does not simply split on "\r\n"
        /// </remarks>
        /// <seealso href="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#simple-types"/>
        /// <param name="resp3BlobStringBytes">The RESP3 string to parse</param>
        /// <returns>The byte[] value embedded in the RESP3 string</returns>
        /// <exception cref="Resp3ProtocolException">If the provided byte[] is malformed</exception>
        internal static ReadOnlySpan<byte> ParseBlobString(byte[] resp3BlobStringBytes)
        {
            int remainingIndex = ParseBlobString(0, resp3BlobStringBytes, out ReadOnlySpan<byte> output);

            if (remainingIndex != resp3BlobStringBytes.Length)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob string: Extra characters found at the end of a valid blob string.");
            }

            return output;
        }

        /// <summary>
        /// Parse a RESP3 blob string formatted byte[] within a byte[] and return the embedded content byte[]
        /// basic form: "{optional extra bytes}${length}\r\n{bytes}\r\n{optional extra bytes}"
        /// example: "someExtraBytesToIgnore$11\r\nhello world\r\nsomeExtraBytesToIgnore"
        /// </summary>
        /// <remarks>
        /// Note that "\r\n" may appear within the {bytes} segment, so this function
        /// does not simply split on "\r\n"
        /// </remarks>
        /// <seealso href="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#simple-types"/>
        /// <param name="startIndex">The index to start parsing the blob array from.</param>
        /// <param name="resp3BlobStringBytes">The RESP3 string to parse</param>
        /// <param name="blobString">The parsed blob string that will be returned</param>
        /// <returns>The index of <paramref name="resp3BlobStringBytes"/> after the first blob string from the provided <paramref name="startIndex"/>.</returns>
        /// <exception cref="Resp3ProtocolException">If the provided byte[] is malformed</exception>
        internal static int ParseBlobString(int startIndex, byte[] resp3BlobStringBytes, out ReadOnlySpan<byte> blobString)
        {
            if (resp3BlobStringBytes == null || resp3BlobStringBytes.Length < 3)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob string");
            }

            if (resp3BlobStringBytes[startIndex] != Encoding.ASCII.GetBytes("$")[0])
            {
                ThrowIfSimpleError(resp3BlobStringBytes);
                throw new Resp3ProtocolException("Invalid RESP3 blob string: Doesn't begin with '$' character");
            }

            byte[] blobStringLengthSegment;
            int remainingIndex = ReadUntilSeperator(resp3BlobStringBytes, startIndex + 1, out blobStringLengthSegment);

            string blobStringLengthString;
            try
            {
                blobStringLengthString = Encoding.ASCII.GetString(blobStringLengthSegment);
            }
            catch (DecoderFallbackException)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob string: non-ASCII characters detected.");
            }

            if (!int.TryParse(blobStringLengthString, out int declaredLength))
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob string: length segment could not be parsed as an integer");
            }

            int totalBlobStringLength = "$".Length + blobStringLengthString.Length + Separator.Length + declaredLength + Separator.Length;

            // Parse the remaining "{length}\r\n" portion of the overall blob string
            if (resp3BlobStringBytes.Length - remainingIndex < declaredLength + Separator.Length)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob string: the blob string's actual length does not match its declared length");
            }

            if (resp3BlobStringBytes[startIndex + totalBlobStringLength - 2] != Encoding.ASCII.GetBytes(Separator)[0]
                || resp3BlobStringBytes[startIndex + totalBlobStringLength - 1] != Encoding.ASCII.GetBytes(Separator)[1])
            {
                throw new Resp3ProtocolException($"Invalid RESP3 object: missing the final \"\\r\\n\" separators");
            }

            blobString = new ReadOnlySpan<byte>(resp3BlobStringBytes, remainingIndex, declaredLength);

            return startIndex + totalBlobStringLength;
        }

        /// <summary>
        /// Parse a RESP3 blob array formatted byte[] and return the embedded content byte[] segments
        /// basic form: "*{arrayLength}\r\n${segment1Length}\r\n{segment1bytes}\r\n${segment2Length}\r\n{segment2bytes}\r\n..."
        /// example: "*2\r\n$11\r\nhello world\r\n$7\r\ngoodbye\r\n"
        /// </summary>
        /// <remarks>
        /// Note that "\r\n" may appear within each of the {bytes} segments, so this function
        /// does not simply split on "\r\n"
        /// </remarks>
        /// <seealso href="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#aggregate-data-types-overview"/>
        /// <param name="resp3BlobArrayBytes">The RESP3 blob array to parse</param>
        /// <returns>The segments of the blob array in order.</returns>
        /// <exception cref="Resp3ProtocolException">If the provided byte[] is malformed</exception>
        internal static List<byte[]> ParseBlobArray(byte[] resp3BlobArrayBytes)
        {
            if (resp3BlobArrayBytes.Length < 4)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob array: must start with \'*<array length>\r\n\'.");
            }

            if (resp3BlobArrayBytes[0] != (byte)'*')
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob array: must start with \'*\'");
            }

            int remainingIndex = ReadUntilSeperator(resp3BlobArrayBytes, 1, out byte[] blobArraySize);

            if (blobArraySize.Length < 1)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob array: missing array length");
            }

            string blobStringLengthString;
            try
            {
                blobStringLengthString = Encoding.ASCII.GetString(blobArraySize);
            }
            catch (DecoderFallbackException)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob array: non-ASCII characters detected in the array length segment.");
            }

            if (!int.TryParse(blobStringLengthString, out int declaredArrayLength))
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob array: array size segment could not be parsed as an integer");
            }

            List<byte[]> blobStrings = new();
            for (int blobStringIndex = 0; blobStringIndex < declaredArrayLength; blobStringIndex++)
            {
                if (remainingIndex >= resp3BlobArrayBytes.Length)
                {
                    throw new Resp3ProtocolException("Invalid RESP3 blob array: declared array size does not match the actual size.");
                }

                try
                {
                    remainingIndex = ParseBlobString(remainingIndex, resp3BlobArrayBytes, out ReadOnlySpan<byte> blobString);
                    blobStrings.Add(blobString.ToArray());
                }
                catch (Resp3ProtocolException e)
                {
                    throw new Resp3ProtocolException("Invalid RESP3 blob array: one or more array elements is not a valid blob string", e);
                }
            }

            if (remainingIndex < resp3BlobArrayBytes.Length)
            {
                throw new Resp3ProtocolException("Invalid RESP3 blob array: unexpected characters at the end of a valid blob array.");
            }

            return blobStrings;
        }

        /// <summary>
        /// Parse a RESP3 number formatted byte[] and return the number
        /// basic form: ":{number}\r\n"
        /// for example: ":1234\r\n"
        /// </summary>
        /// <seealso href="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#simple-types"/>
        /// <param name="resp3NumberBytes">The RESP3 string to parse</param>
        /// <returns>The number embedded in the RESP3 string</returns>
        /// <exception cref="ArgumentException">If the provided byte[] is malformed</exception>
        internal static int ParseNumber(byte[] resp3NumberBytes)
        {
            if (resp3NumberBytes == null || resp3NumberBytes.Length < 4)
            {
                throw new Resp3ProtocolException("Invalid RESP3 number");
            }

            string resp3Number;
            try
            {
                resp3Number = Encoding.ASCII.GetString(resp3NumberBytes);
            }
            catch (DecoderFallbackException)
            {
                throw new Resp3ProtocolException("Invalid RESP3 number: non-ASCII characters detected.");
            }

            if (!resp3Number.StartsWith(":"))
            {
                ThrowIfSimpleError(resp3NumberBytes);
                throw new Resp3ProtocolException("Invalid RESP3 number: missing \":\" character at the beginning of the string");
            }

            // Strip away the initial ":" character so that all that remains is
            //"{number}\r\n"
            string remainingNumberWithSeperator = resp3Number.Substring(1);

            if (remainingNumberWithSeperator.LastIndexOf(Separator) != remainingNumberWithSeperator.Length - Separator.Length)
            {
                throw new Resp3ProtocolException($"Invalid RESP3 number: {Separator} missing or not at the end of the string");
            }

            // Strip away the final "\r\n" characters so that all that remains is
            //"{number}"
            string remainingNumber = remainingNumberWithSeperator.Substring(0, remainingNumberWithSeperator.Length - Separator.Length);
            if (!int.TryParse(remainingNumber, out int number))
            {
                throw new Resp3ProtocolException($"Invalid RESP3 number: number contents cannot be parsed to int");
            }

            return number;
        }

        /// <summary>
        /// Parse a RESP3 number formatted byte[] and return the string
        /// basic form: "+{string}\r\n"
        /// for example: "+hello world\r\n"
        /// </summary>
        /// <seealso href="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#simple-types"/>
        /// <param name="resp3SimpleStringBytes">The RESP3 string to parse</param>
        /// <returns>The simple string embedded in the RESP3 string</returns>
        /// <exception cref="ArgumentException">If the provided byte[] is malformed</exception>
        internal static string ParseSimpleString(byte[] resp3SimpleStringBytes)
        {
            if (resp3SimpleStringBytes == null || resp3SimpleStringBytes.Length < 4)
            {
                throw new Resp3ProtocolException("Invalid RESP3 simple string");
            }

            string resp3SimpleString;
            try
            {
                resp3SimpleString = Encoding.ASCII.GetString(resp3SimpleStringBytes);
            }
            catch (DecoderFallbackException)
            {
                throw new Resp3ProtocolException("Invalid RESP3 simple string: non-ASCII characters detected.");
            }

            if (!resp3SimpleString.StartsWith("+"))
            {
                ThrowIfSimpleError(resp3SimpleStringBytes);
                throw new Resp3ProtocolException("Invalid RESP3 simple string: missing \"+\" character at the beginning of the string");
            }

            // Strip away the initial ":" character so that all that remains is
            //"{string}\r\n"
            string remainingStringWithSeperator = resp3SimpleString.Substring(1);

            if (remainingStringWithSeperator.LastIndexOf(Separator) != remainingStringWithSeperator.Length - Separator.Length)
            {
                throw new Resp3ProtocolException($"Invalid RESP3 number: {Separator} missing or not at the end of the string");
            }

            // Strip away the final "\r\n" characters so that all that remains is
            //"{string}"
            string simpleString = remainingStringWithSeperator.Substring(0, remainingStringWithSeperator.Length - Separator.Length);
            return simpleString;
        }

        /// <summary>
        /// Build a RESP3 byte[] for an array of RESP3 objects.
        /// form: "*{arrayLength}\r\n{element1}{element2}..." where each element is another valid RESP3 object
        /// such as a simple string, a blob string, a number, or even another array
        /// For example: "*3\r\n+someSimpleString\r\n:1\r\n:200\r\n"
        /// which can be read as ["someSimpleString", 1, 200]
        /// </summary>
        /// <seealso href="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#aggregate-data-types-overview"/>
        /// <param name="resp3Objects">The RESP3 byte[] objects to include in the array</param>
        /// <returns>The RESP3 array byte[]</returns>
        internal static byte[] BuildArray(params byte[][] resp3Objects)
        {
            if (resp3Objects.Length < 1)
            {
                throw new ArgumentException("Array must contain at least one object");
            }

            var numArgs = resp3Objects.Length;
            var arrayBuilder = new List<byte>();

            arrayBuilder.AddRange(Encoding.ASCII.GetBytes($"*{numArgs}{Separator}"));
            foreach (byte[] resp3Object in resp3Objects)
            {
                arrayBuilder.AddRange(resp3Object);
            }

            return arrayBuilder.ToArray();
        }

        /// <summary>
        /// Build a RESP3 blob string that contains the provided value.
        /// basic form: "${length}\r\n{bytes}\r\n"
        /// example: "$11\r\nhello world\r\n"
        /// </summary>
        /// <param name="value">The value to embed in the RESP3 blob string</param>
        /// <returns>The RESP3 blob string</returns>
        internal static byte[] BuildBlobString(byte[] value)
        {
            if (value == null)
            {
                throw new Resp3ProtocolException("Value must be non-null");
            }

            List<byte> blobStringBuilder =
            [
                .. Encoding.ASCII.GetBytes($"${value.Length}"),
                .. Encoding.ASCII.GetBytes($"{Separator}"),
                .. value,
                .. Encoding.ASCII.GetBytes($"{Separator}"),
            ];

            return blobStringBuilder.ToArray();
        }

        /// <summary>
        /// Check if a payload is a Nil payload.
        /// </summary>
        /// <remarks>
        /// Nil is defined as "$-1\r\n"
        /// </remarks>
        /// <seealso href="https://redis.io/docs/reference/protocol-spec/#null-bulk-strings"/>
        /// <param name="value">The payload to check.</param>
        /// <returns>True if it is a Nil payload and false otherwise.</returns>
        internal static bool IsNil(byte[] value)
        {
            return Enumerable.SequenceEqual(value, Nil1)
                || Enumerable.SequenceEqual(value, Nil2);
        }

        internal static void ThrowIfSimpleError(byte[] payload)
        {
            if (payload.Length < 5)
            {
                return;
            }

            if (payload[0] != Error[0]
                || payload[1] != Error[1]
                || payload[2] != Error[2]
                || payload[3] != Error[3]
                || payload[4] != Encoding.ASCII.GetBytes(" ")[0])
            {
                // payload doesn't start with "-ERR " so it isn't a simple error
                return;
            }

            if (payload[payload.Length - 2] != Separator[0]
                || payload[payload.Length - 1] != Separator[1])
            {
                // payload doesn't end with "\r\n", so it isn't a simple error
                return;
            }

            ReadUntilSeperator(payload, 5, out byte[] description);
            throw new Resp3SimpleErrorException(Encoding.ASCII.GetString(description));
        }

        private static int ReadUntilSeperator(byte[] source, int startIndex, out byte[] destination)
        {
            int index = startIndex;
            byte readByte = source[index];
            List<byte> readBytes = new List<byte>();
            // Read the byte[] until the beginning of the \r\n
            while (readByte != Separator[0])
            {
                readBytes.Add(readByte);
                index++;

                try
                {
                    readByte = source[index];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new Resp3ProtocolException($"Invalid RESP3 object: missing one or more \"\\r\\n\" separators");
                }
            }

            // Check that there is a \r\n at the end
            try
            {
                if ((source[index] != Encoding.ASCII.GetBytes(Separator)[0])
                    || source[++index] != Encoding.ASCII.GetBytes(Separator)[1])
                {
                    throw new Resp3ProtocolException($"Invalid RESP3 object: missing one or more \"\\r\\n\" separators");
                }
            }
            catch (IndexOutOfRangeException)
            {
                throw new Resp3ProtocolException($"Invalid RESP3 object: missing one or more \"\\r\\n\" separators");
            }

            destination = readBytes.ToArray();

            return ++index;
        }
    }
}
