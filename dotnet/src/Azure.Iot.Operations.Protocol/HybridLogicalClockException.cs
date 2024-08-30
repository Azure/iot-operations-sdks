using System;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// The exception type that is thrown when updating a <see cref="HybridLogicalClock"/>
    /// if an error occurs.
    /// </summary>
    public class HybridLogicalClockException : Exception
    {
        public HybridLogicalClockException() { }

        public HybridLogicalClockException(string message) : base(message) { }
    }
}
