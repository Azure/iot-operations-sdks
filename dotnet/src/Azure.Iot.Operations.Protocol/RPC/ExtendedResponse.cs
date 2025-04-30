// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public struct ExtendedResponse<TResp>
        where TResp : class
    {
        // These two user properties are used to communicate application level errors in an RPC response message. Code is mandatory, but data is optional.
        public const string ApplicationErrorCodeUserDataKey = "AppErrCode";
        public const string ApplicationErrorPayloadUserDataKey = "AppErrPayload";

        public TResp Response { get; set; }

        public CommandResponseMetadata? ResponseMetadata { get; set; }

        public ExtendedResponse<TResp> WithApplicationError(string errorCode)
        {
            ResponseMetadata ??= new();
            SetApplicationError(errorCode, null);
            return this;
        }

        public ExtendedResponse<TResp> WithApplicationError(string errorCode, string? errorPayload)
        {           
            ResponseMetadata ??= new();
            SetApplicationError(errorCode, errorPayload);
            return this;
        }

        public bool TryGetApplicationError(out string? errorCode, out string? errorPayload)
        {
            errorCode = null;
            errorPayload = null;

            if (ResponseMetadata == null || ResponseMetadata.UserData == null || !ResponseMetadata.UserData.TryGetValue(ApplicationErrorCodeUserDataKey, out string? code) || code == null)
            {
                return false;
            }

            errorCode = code;

            if (ResponseMetadata != null && ResponseMetadata.UserData != null && ResponseMetadata.UserData.TryGetValue(ApplicationErrorPayloadUserDataKey, out string? errorPayloadString) && errorPayloadString != null)
            {
                errorPayload = errorPayloadString;
            }

            return true;
        }

        public bool IsApplicationError()
        {
            return ResponseMetadata?.UserData != null && ResponseMetadata != null && ResponseMetadata.UserData.ContainsKey(ApplicationErrorCodeUserDataKey);
        }

        private void SetApplicationError(string applicationErrorCode, string? errorPayload)
        {
            ResponseMetadata ??= new();
            ResponseMetadata.UserData ??= new();
            ResponseMetadata.UserData[ApplicationErrorCodeUserDataKey] = applicationErrorCode;

            if (errorPayload != null)
            {
                try
                {
                    ResponseMetadata.UserData[ApplicationErrorPayloadUserDataKey] = errorPayload;
                }
                catch (Exception e)
                {
                    throw new AkriMqttException("Failed to serialize the application error", e)
                    {
                        IsRemote = true,
                        Kind = AkriMqttErrorKind.PayloadInvalid,
                        IsShallow = false,
                    };
                }
            }
        }
    }
}
