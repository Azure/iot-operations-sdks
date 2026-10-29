// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    // Builds and reads the Failed status (ADR 25, stream failure): s:0, __stat 500, an optional __stMsg, and for an
    // application error the ADR 21 headers AppErrCode / AppErrPayload. __apErr is never set on streaming messages.
    internal static class StreamFailure
    {
        public const string FailedStatusCode = "500";
        public const string AppErrCode = "AppErrCode";
        public const string AppErrPayload = "AppErrPayload";

        // Adds the Failed status properties describing a producer's failure to an outgoing status message.
        public static void AddFailedProperties(MqttApplicationMessage message, Exception error)
        {
            message.AddUserProperty(AkriSystemProperties.Status, FailedStatusCode);
            message.AddUserProperty(AkriSystemProperties.StatusMessage, error.Message);

            if (error is StreamingApplicationException appError)
            {
                message.AddUserProperty(AppErrCode, appError.ErrorCode);
                if (appError.ErrorPayload != null)
                {
                    message.AddUserProperty(AppErrPayload, appError.ErrorPayload);
                }
            }
        }

        // Reads a received status message; returns the error to report to the consumer when it is a Failed status.
        public static bool TryReadFailure(MqttApplicationMessage message, Guid correlationId, out Exception error)
        {
            error = null!;
            if (GetProperty(message, AkriSystemProperties.Status) != FailedStatusCode)
            {
                return false;
            }

            string? statusMessage = GetProperty(message, AkriSystemProperties.StatusMessage);
            string? appErrCode = GetProperty(message, AppErrCode);

            error = appErrCode != null
                ? new StreamingApplicationException(appErrCode, GetProperty(message, AppErrPayload), statusMessage) { IsRemote = true }
                : new AkriMqttException(statusMessage ?? "The peer's stream failed.")
                {
                    Kind = AkriMqttErrorKind.ExecutionException,
                    IsShallow = false,
                    IsRemote = true,
                    CorrelationId = correlationId,
                };
            return true;
        }

        private static string? GetProperty(MqttApplicationMessage message, string name) =>
            message.UserProperties?.FirstOrDefault(p => p.Name == name)?.Value;
    }
}
