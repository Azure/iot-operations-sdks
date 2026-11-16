// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    // Builds and reads the terminal statuses a producer or consumer sends to end the exchange (ADR 25):
    // - Failed (stream failure): s:0, __stat 500, an optional __stMsg, and for an application error the ADR 21 headers
    //   AppErrCode / AppErrPayload. __apErr is never set on streaming messages.
    // - Overflow (flow control): s:<index of the entry that did not fit>, __stat 429, an optional __stMsg.
    internal static class StreamFailure
    {
        public const string FailedStatusCode = "500";
        public const string OverflowStatusCode = "429";
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

        // Adds the Overflow status properties to an outgoing status message whose __stream is s:<index>.
        public static void AddOverflowProperties(MqttApplicationMessage message, StreamOverflowException overflow)
        {
            message.AddUserProperty(AkriSystemProperties.Status, OverflowStatusCode);
            message.AddUserProperty(AkriSystemProperties.StatusMessage, overflow.Message);
        }

        // Reads a received status message; returns the error to report to the consumer when it is a Failed or Overflow status.
        public static bool TryReadTerminalStatus(MqttApplicationMessage message, Guid correlationId, uint statusIndex, out Exception error)
        {
            error = null!;
            string? status = GetProperty(message, AkriSystemProperties.Status);
            string? statusMessage = GetProperty(message, AkriSystemProperties.StatusMessage);

            if (status == OverflowStatusCode)
            {
                error = new StreamOverflowException(statusIndex, statusMessage) { IsRemote = true };
                return true;
            }

            if (status != FailedStatusCode)
            {
                return false;
            }

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

        // Producer pacing (ADR 25, flow control): a message the broker rejects ends the exchange with a Failed status.
        public static void EnsureAccepted(MqttClientPublishResult result, string what)
        {
            if (!result.IsSuccess)
            {
                throw new AkriMqttException($"The broker did not accept the {what} ({result.ReasonCode}).")
                {
                    Kind = AkriMqttErrorKind.MqttError,
                    IsShallow = false,
                    IsRemote = false,
                };
            }
        }

        // The error a side reports when its exchange timeout fires before graceful completion.
        public static AkriMqttException Timeout(Guid correlationId, uint timeoutSeconds) =>
            new($"The streaming exchange timed out after {timeoutSeconds} s.")
            {
                Kind = AkriMqttErrorKind.Timeout,
                IsShallow = false,
                IsRemote = false,
                CorrelationId = correlationId,
                TimeoutName = "ExchangeTimeout",
                TimeoutValue = TimeSpan.FromSeconds(timeoutSeconds),
            };

        private static string? GetProperty(MqttApplicationMessage message, string name) =>
            message.UserProperties?.FirstOrDefault(p => p.Name == name)?.Value;
    }
}
