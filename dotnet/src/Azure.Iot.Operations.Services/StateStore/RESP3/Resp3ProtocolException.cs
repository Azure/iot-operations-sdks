﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore.RESP3
{
    /// <summary>
    /// An exception that is thrown when a protocol violation is found when
    /// reading/writing RESP3 payload objects.
    /// </summary>
    public class Resp3ProtocolException : Exception
    {
        public Resp3ProtocolException(string message) : base(message)
        { }

        public Resp3ProtocolException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
