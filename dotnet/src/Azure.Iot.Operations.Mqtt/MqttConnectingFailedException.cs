// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt
{
    public sealed class MqttConnectingFailedException : Exception
    {
        public MqttConnectingFailedException(string message, MqttClientConnectResult connectResult)
            : base(message)
        {
            Result = connectResult;
        }

        public MqttClientConnectResult Result { get; }

        public MqttClientConnectResultCode ResultCode => Result?.ResultCode ?? MqttClientConnectResultCode.UnspecifiedError;
    }
}
