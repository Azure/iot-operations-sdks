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
    public class AdrClientWrapperFactoryProvider : IAdrClientWrapperFactoryProvider
    {
        public static Func<IServiceProvider, IAdrClientWrapperFactoryProvider> Factory = service =>
        {
            return new AdrClientWrapperFactoryProvider();
        };

        public IAdrClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
        {
            return new AdrClientWrapper(applicationContext, mqttPubSubClient);
        }
    }
}
