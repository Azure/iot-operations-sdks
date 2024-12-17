// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.RPC
{
    public struct ExtendedResponse<TResp>
        where TResp : class
    {
        public TResp Response { get; set; }

        public CommandResponseMetadata? ResponseMetadata { get; set; }

#pragma warning disable CA1000 // Do not declare static members on generic types
        public static ExtendedResponse<TResp> CreateFromResponse(TResp response)
        {
            return new()
            {
                Response = response,
                ResponseMetadata = null,
            };
        }
#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
