// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol;

public interface IExtendedPubSubMqttClient : IMqttPubSubClient
{
    MqttClientConnectResult? GetConnectResult();
}
