// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

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
            object? payload = null;
            SetApplicationError(errorCode, payload, null);
            return this;
        }

        public ExtendedResponse<TResp> WithApplicationError<TError>(string errorCode, TError errorPayload, IErrorHeaderPayloadSerializer serializer) where TError : class
        {
            if (errorPayload != null && serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer), "Must provide a serializer if error payload is non-null");
            }

            ResponseMetadata ??= new();
            SetApplicationError(errorCode, errorPayload, serializer);
            return this;
        }

        public bool TryGetApplicationError(out string? errorCode)
        {
            if (ResponseMetadata == null || ResponseMetadata.UserData == null || !ResponseMetadata.UserData.TryGetValue(ApplicationErrorCodeUserDataKey, out string? code) || code == null)
            {
                errorCode = null;
                return false;
            }

            errorCode = code;
            return true;
        }

        public bool TryGetApplicationError<TError>(IErrorHeaderPayloadSerializer? serializer, out string? errorCode, out TError? errorPayload) where TError : class
        {
            if (!TryGetApplicationError(out errorCode))
            {
                errorPayload = null;
                return false;
            }

            errorPayload = null;

            //TODO do we report if a payload was found, but no serializer was provided?
            if (ResponseMetadata != null && ResponseMetadata.UserData != null && ResponseMetadata.UserData.TryGetValue(ApplicationErrorPayloadUserDataKey, out string? errorPayloadString) && errorPayloadString != null && serializer != null)
            {
                errorPayload = serializer.FromString<TError>(errorPayloadString);
            }

            return true;
        }

        public bool IsApplicationError()
        {
            return ResponseMetadata?.UserData != null && ResponseMetadata != null && ResponseMetadata.UserData.ContainsKey(ApplicationErrorCodeUserDataKey);
        }

        private void SetApplicationError<TError>(string applicationErrorCode, TError? errorData, IErrorHeaderPayloadSerializer? serializer) where TError : class
        {
            ResponseMetadata ??= new();
            ResponseMetadata.UserData ??= new();
            ResponseMetadata.UserData[ApplicationErrorCodeUserDataKey] = applicationErrorCode;

            if (errorData != null && serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer), "Must provide a serializer if non-null errorData is provided");
            }

            if (errorData != null && serializer != null)
            {
                try
                {
                    ResponseMetadata.UserData[ApplicationErrorPayloadUserDataKey] = serializer.ToString<TError>(errorData);
                }
                catch (Exception)
                {
                    //TODO log or throw?
                }
            }
        }
    }
}
