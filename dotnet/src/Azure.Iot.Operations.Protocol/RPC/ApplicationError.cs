// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    /// <summary>
    /// A command response that indicates some error occurred within the application layer that handled the command invocation.
    /// </summary>
    public class ApplicationError<T>
    {
        public string Code { get; set; } // user defined code. Should be set when user returns from command executor handler with error

        public T? Payload { get; set; }

        /// <summary>
        /// If any exception was encountered when the command invoker deserialized the payload field.
        /// </summary>
        public Exception? DataSerializationException { get; internal set; }

        internal ApplicationError(string code)
        {
            Code = code;
        }
    }
}
