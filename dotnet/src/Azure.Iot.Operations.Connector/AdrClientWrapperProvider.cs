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
    public class AdrClientWrapperProvider : IAdrClientWrapperProvider
    {
        public static Func<IServiceProvider, IAdrClientWrapperProvider> Factory = service =>
        {
            return new AdrClientWrapperProvider();
        };

        public IAdrClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
        {
            return new AdrClientWrapper(applicationContext, mqttPubSubClient);
        }
    }
}
