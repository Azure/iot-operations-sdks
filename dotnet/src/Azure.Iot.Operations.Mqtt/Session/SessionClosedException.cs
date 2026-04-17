// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Mqtt.Session
{
    public class SessionClosedException : Exception
    {
        public SessionClosedException(string message) : base(message) { }
    }
}
