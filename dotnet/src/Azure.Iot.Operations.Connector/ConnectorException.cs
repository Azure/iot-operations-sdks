// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    //TODO naming?
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
    }
}
