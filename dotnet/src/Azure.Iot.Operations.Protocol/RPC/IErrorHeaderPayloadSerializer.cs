// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.RPC
{
    public interface IErrorHeaderPayloadSerializer
    {
        string ToString<T>(T? payload) where T : class;

        T FromString<T>(string payloadString) where T : class;
    }
}
