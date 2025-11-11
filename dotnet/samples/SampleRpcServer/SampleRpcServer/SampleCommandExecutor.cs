// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;

namespace SampleRpcServer
{
    [CommandTopic("rpc/command-samples/{commandName}")]
    public class SampleCommandExecutor : CommandExecutor<PayloadObject, PayloadObject>
    {
        public SampleCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer) : base(applicationContext, mqttClient, commandName, serializer)
        {
            TopicTokenMap["commandName"] = commandName;
        }
    }
}
