// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseDuration
    {
        public int Hours { get; set; }

        public int Minutes { get; set; }

        public int Seconds { get; set; }

        public int Milliseconds { get; set; }

        public int Microseconds { get; set; }

        public TimeSpan ToTimeSpan()
        {
            return new TimeSpan(0, Hours, Minutes, Seconds, Milliseconds, Microseconds);
        }
    }
}
