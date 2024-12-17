// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.StateStore;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    /// <summary>
    /// A single candidate that may be a leader.
    /// </summary>
    public class LeaderElectionCandidate : IEquatable<LeaderElectionCandidate>
    {
#pragma warning disable CA1819 // Properties should not return arrays
        // While this rule is generally good to follow, the State Store is designed to handle byte[]
        // so this client should as well.
        public byte[] Bytes { get; }
#pragma warning restore CA1819 // Properties should not return arrays

        internal LeaderElectionCandidate(byte[] bytes)
        {
            Bytes = bytes;
        }

        internal LeaderElectionCandidate(string value)
        {
            Bytes = Encoding.UTF8.GetBytes(value);
        }

        public string GetString()
        {
            return Encoding.UTF8.GetString(Bytes);
        }

        public bool Equals(LeaderElectionCandidate? other)
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

            if (other is not LeaderElectionCandidate)
            {
                return false;
            }

            return Enumerable.SequenceEqual(Bytes, ((LeaderElectionCandidate)other).Bytes);
        }

        public override int GetHashCode()
        {
            return Bytes.GetHashCode();
        }

        public static implicit operator LeaderElectionCandidate?(string? value)
        {
            if (value == null || value.Length == 0)
            {
                return new LeaderElectionCandidate(string.Empty);
            }

            return new LeaderElectionCandidate(value);
        }
    }
}
