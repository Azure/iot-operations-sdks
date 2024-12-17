// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore.RESP3
{
    /// <summary>
    /// An exception that is thrown when this client receives a RESP3 simple error from the
    /// service. The error description is a human-readable error message that explains what went
    /// wrong.
    /// </summary>
    /// <seealso cref="https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md#simple-types"/>
    public class Resp3SimpleErrorException : Exception
    {
        public string ErrorDescription { get; set; }

        public Resp3SimpleErrorException(string errorDescription)
            : base(errorDescription)
        {
            ErrorDescription = errorDescription;
        }
    }
}