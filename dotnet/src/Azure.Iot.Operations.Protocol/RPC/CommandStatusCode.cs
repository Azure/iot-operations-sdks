// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.RPC
{
    /// <summary>
    /// HTTP status codes used to indicate the status of a Command.
    /// </summary>
    public enum CommandStatusCode : int
    {
        /// <summary>OK. No error.</summary>
        OK = 200,

        /// <summary>No Content. There is no content to send for this request.</summary>
        NoContent = 204,

        /// <summary>Bad Request. Header or payload is missing or invalid.</summary>
        BadRequest = 400,

        /// <summary>Request Timeout. The request timeout out before a response could be received from the command processor.</summary>
        RequestTimeout = 408,

        /// <summary>Unsupported Media Type. The content type specified in the request is not supported by this implementation.</summary>
        UnsupportedMediaType = 415,

        /// <summary>Unprocessable Content. The request was well-formed but was unable to be followed due to semantic errors, as indicated via an <see cref="InvocationException"/>.</summary>
        UnprocessableContent = 422,

        /// <summary>Internal Server.  Unknown error, internal logic error, or command processor error other than <see cref="InvocationException"/>.</summary>
        InternalServerError = 500,

        /// <summary>Service Unavailable.  Invalid service state preventing command from executing properly.</summary>
        ServiceUnavailable = 503,

        /// <summary> The request failed because the remote party did not support the requested protocol version.</summary>
        NotSupportedVersion = 505,
    }
}
