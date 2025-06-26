// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class StreamingExtendedResponse<TResp> : ExtendedResponse<TResp>
        where TResp : class
    {
    }
}
