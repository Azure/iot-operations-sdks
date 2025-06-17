// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Connector
{
    public interface IAdrClientWrapperProvider
    {
        IAdrClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient);
    }
}
