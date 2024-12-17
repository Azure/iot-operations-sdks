// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttClientChannelOptions
    {
        MqttClientTlsOptions TlsOptions { get; }
    }
}
