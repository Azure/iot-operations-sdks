// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

namespace Azure.Iot.Operations.Services.StateStore
{
    public abstract class StateStoreObject : IEquatable<StateStoreObject>
    {
#pragma warning disable CA1819 // Properties should not return arrays
        // While this rule is generally good to follow, the State Store is designed to handle byte[]
        // so this client should as well.
        public byte[] Bytes { get; }
#pragma warning restore CA1819 // Properties should not return arrays

        public StateStoreObject(string value)
        {
            Bytes = Encoding.UTF8.GetBytes(value);
        }

        public StateStoreObject(byte[] value)
        {
            Bytes = value;
        }

        public StateStoreObject(Stream value)
        {
            //TODO try deferring the reading of the stream for later
            var payloadBuffer = new byte[value.Length];
            var totalRead = 0;
            do
            {
                var bytesRead = value.Read(payloadBuffer, totalRead, payloadBuffer.Length - totalRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            } while (totalRead < value.Length);

            Bytes = payloadBuffer;
        }

        public StateStoreObject(Stream value, long length)
        {
            //TODO try deferring the reading of the stream for later
            var payloadBuffer = new byte[length];
            var totalRead = 0;
            do
            {
                var bytesRead = value.Read(payloadBuffer, totalRead, payloadBuffer.Length - totalRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            } while (totalRead < length);

            Bytes = payloadBuffer;
        }

        public StateStoreObject(ArraySegment<byte> value)
        {
            if (value.Array == null)
            {
                Bytes = Array.Empty<byte>();
            }
            else
            {
                Bytes = value.ToArray();
            }
        }

        public StateStoreObject(IEnumerable<byte> value)
        {
            if (value is byte[] byteArray)
            {
                Bytes = byteArray;
            }
            else if (value is ArraySegment<byte> arraySegment)
            {
                if (arraySegment.Array == null)
                {
                    Bytes = Array.Empty<byte>();
                }
                else
                { 
                    Bytes = arraySegment.Array;
                }
            }
            else 
            { 
                Bytes = value.ToArray();
            }
        }

        public string GetString()
        {
            return Encoding.UTF8.GetString(Bytes);
        } 

        public bool Equals(StateStoreObject? other)
        {
            if (other == null)
            { 
                return false;
            }

            return Enumerable.SequenceEqual(Bytes, other.Bytes);
        }

        public override bool Equals(object? other)
        {
            if (other == null)
            {
                return false;
            }
            
            if (other is string)
            {
                try
                {
                    return string.Equals(GetString(), (string)other);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (other is not StateStoreObject)
            {
                return false;
            }

            return Enumerable.SequenceEqual(Bytes, ((StateStoreObject)other).Bytes);
        }

        public override int GetHashCode()
        {
            return Bytes.GetHashCode();
        }

        public override string ToString()
        {
            return GetString();
        }
    }
}
