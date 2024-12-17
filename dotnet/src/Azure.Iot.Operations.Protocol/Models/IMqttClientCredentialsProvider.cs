// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttClientCredentialsProvider
    {
        string GetUserName(MqttClientOptions clientOptions);

        byte[]? GetPassword(MqttClientOptions clientOptions);
    }
}
