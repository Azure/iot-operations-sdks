// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.RPC
{
    public struct ExtendedRequest<TReq>
        where TReq : class
    {
        public TReq Request { get; set; }

        public CommandRequestMetadata RequestMetadata { get; set; }
    }
}
