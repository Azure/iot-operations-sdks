// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttEnhancedAuthenticationHandler
    {
        Task HandleEnhancedAuthenticationAsync(MqttEnhancedAuthenticationEventArgs eventArgs);
    }
}
