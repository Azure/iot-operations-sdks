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
    /// <summary>
    /// An exception that indicates a failure to sample an asset due to a failure to connect to or get a response from an asset.
    /// </summary>
    public class AssetSamplingException : ConnectorException
    {
        public AssetSamplingException()
        {
        }

        public AssetSamplingException(string? message) : base(message)
        {
        }

        public AssetSamplingException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
