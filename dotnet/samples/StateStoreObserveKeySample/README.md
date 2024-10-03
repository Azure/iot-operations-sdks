# State Store Observe Key Sample

This sample demonstrates notification of state changes in the Distributed State Store.

## Prerequisites

Before running this sample, deploy a Kubernetes cluster with the latest MQ bits.

> :NOTE:
>  This sample code assumes the MQ instance is running on localhost and exposes port 1883.

```csharp
var connectionSettings = new MqttClientOptionsBuilder()
        .WithTcpServer("localhost", 1883)
        .WithClientId("someClientId")
        .WithProtocolVersion(MqttProtocolVersion.V500)
        .Build();
```


