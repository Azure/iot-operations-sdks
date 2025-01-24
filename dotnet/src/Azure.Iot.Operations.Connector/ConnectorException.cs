// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorException : Exception
    {
        public ConnectorException()
        {
        }

        public ConnectorException(string? message) : base(message)
        {
        }

        public ConnectorException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected ConnectorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
