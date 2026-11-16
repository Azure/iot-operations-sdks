// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// An application error that ends a stream (ADR 25, stream failure). A producer raises it from the stream it supplies
    /// to end that stream with a <c>Failed</c> status carrying the ADR 21 headers <c>AppErrCode</c> and <c>AppErrPayload</c>;
    /// a consumer receives it, with <see cref="IsRemote"/> set, when the peer ends its stream that way.
    /// </summary>
    public class StreamingApplicationException : Exception
    {
        public StreamingApplicationException(string errorCode, string? errorPayload = null, string? message = null)
            : base(message ?? $"The stream failed with application error '{errorCode}'.")
        {
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            ErrorPayload = errorPayload;
        }

        /// <summary>The application-defined error code, carried as <c>AppErrCode</c>.</summary>
        public string ErrorCode { get; }

        /// <summary>The optional application-defined error payload, carried as <c>AppErrPayload</c>.</summary>
        public string? ErrorPayload { get; }

        /// <summary><c>true</c> when the error was reported by the peer rather than raised locally.</summary>
        public bool IsRemote { get; init; }
    }
}
