// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class ExtendedResponse<TResp>
        where TResp : class
    {
        public TResp? Response { get; set; }

        public CommandResponseMetadata? ResponseMetadata { get; set; }

        /*
        public string? ErrorCode
        {
            get { return ResponseMetadata?.UserData?[CommandResponseMetadata.ApplicationErrorCodeUserDataKey]; }
            set
            {
                ArgumentNullException.ThrowIfNullOrWhiteSpace(value, nameof(ErrorCode));
                ResponseMetadata ??= new();
                ResponseMetadata.UserData ??= new();
                ResponseMetadata.UserData[CommandResponseMetadata.ApplicationErrorCodeUserDataKey] = value;
            }
        }
        */
#pragma warning disable CA1000 // Do not declare static members on generic types
        public static ExtendedResponse<TResp> CreateFromResponse(TResp response)
        {
            return new()
            {
                Response = response,
                ResponseMetadata = null,
            };
        }

        public static ExtendedResponse<TResp> CreateExtendedResponseWithApplicationError(TResp response, string errorCode)
        {
            ExtendedResponse<TResp> extendedResponse = new()
            {
                Response = response,
                ResponseMetadata = new()
            };

            object? payload = null;
            extendedResponse.ResponseMetadata.SetApplicationError(errorCode, payload, null);

            return extendedResponse;
        }

        public static ExtendedResponse<TResp> CreateExtendedResponseWithApplicationError<TError>(TResp response, string errorCode, TError errorPayload, IErrorHeaderPayloadSerializer serializer) where TError : class
        {
            if (errorPayload != null && serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer), "Must provide a serializer if error payload is non-null");
            }

            ExtendedResponse<TResp> extendedResponse = new()
            {
                Response = response,
                ResponseMetadata = new()
            };

            extendedResponse.ResponseMetadata.SetApplicationError(errorCode, errorPayload, serializer);

            return extendedResponse;
        }
#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
