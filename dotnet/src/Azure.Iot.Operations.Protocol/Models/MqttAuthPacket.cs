// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttAuthPacket
    {
        public byte[]? AuthenticationData { get; set; }

        public string? AuthenticationMethod { get; set; }

        public MqttAuthenticateReasonCode ReasonCode { get; set; }

        public string? ReasonString { get; set; }

        public List<MqttUserProperty>? UserProperties { get; set; }
    }
}
