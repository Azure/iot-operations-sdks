# Event Driven TCP Thermostat Connector

This sample demonstrates a connector that reacts to events sent to it by an asset. In this case, the asset is a TCP service that will randomly send thermostat data on a TCP connection that is established once the asset is discovered.

Use the [provided script](./setup-cluster.sh) to deploy a kubernetes cluster and this sample code or use [this script](./deploy-connector-and-asset.sh) to deploy just this sample code to an existing kubernetes cluster.