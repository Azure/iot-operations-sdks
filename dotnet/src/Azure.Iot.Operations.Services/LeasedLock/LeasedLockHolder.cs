using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.Iot.Operations.Services.LeasedLock
{
    /// <summary>
    /// A single entity that may hold a leased lock.
    /// </summary>
    public class LeasedLockHolder : IEquatable<LeasedLockHolder>
    {
#pragma warning disable CA1819 // Properties should not return arrays
        // While this rule is generally good to follow, the State Store is designed to handle byte[]
        // so this client should as well.
        public byte[] Bytes { get; }
#pragma warning restore CA1819 // Properties should not return arrays

        internal LeasedLockHolder(byte[] bytes)
        {  
            Bytes = bytes; 
        }

        internal LeasedLockHolder(string value)
        {
            Bytes = Encoding.UTF8.GetBytes(value);
        }

        public string GetString()
        {
            return Encoding.UTF8.GetString(Bytes);
        }

        public bool Equals(LeasedLockHolder? other)
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

            if (other is not LeasedLockHolder)
            {
                return false;
            }

            return Enumerable.SequenceEqual(Bytes, ((LeasedLockHolder)other).Bytes);
        }

        public override int GetHashCode()
        {
            return Bytes.GetHashCode();
        }

        public static implicit operator LeasedLockHolder(string value)
        {
            if (value == null || value.Length == 0)
            {
                return new LeasedLockHolder(string.Empty);
            }

            return new LeasedLockHolder(value);
        }
    }
}
