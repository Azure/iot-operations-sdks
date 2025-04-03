// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public struct ExtendedResponse<TResp>
        where TResp : class
    {
        public TResp Response { get; set; }

        public CommandResponseMetadata? ResponseMetadata { get; set; }

        public ExtendedResponse<TResp> WithApplicationError(string errorCode)
        {
            ResponseMetadata ??= new();
            object? payload = null;
            ResponseMetadata.SetApplicationError(errorCode, payload, null);
            return this;
        }

        public ExtendedResponse<TResp> WithApplicationError<TError>(string errorCode, TError errorPayload, IErrorHeaderPayloadSerializer serializer) where TError : class
        {
            if (errorPayload != null && serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer), "Must provide a serializer if error payload is non-null");
            }

            ResponseMetadata ??= new();
            ResponseMetadata.SetApplicationError(errorCode, errorPayload, serializer);
            return this;
        }
    }
}
