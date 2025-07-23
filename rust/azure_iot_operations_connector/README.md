# Instructions to deploy sample Rust connector

### The sample Rust connector that retrieves ADR definitions is currently under the `examples` folder of this crate.

To deploy follow these steps (from the root of the crate):

1. Create a binary release of the connector code: `cargo build --release --target-dir target --example connector_get_adr_definitions`
2. Build the docker container: `docker build -t connectorgetadrdefinitions:latest -f examples/connector_get_adr_definitions_resources/Dockerfile .`
3. Import into your kubernetes cluster with: `k3d image import connectorgetadrdefinitions:latest`
4. Apply the connector template: `kubectl apply -f examples/connector_get_adr_definitions_resources/connector_template.yaml`
5. Deploy a sample device: `kubectl apply -f examples/connector_get_adr_definitions_resources/thermostat-device-definition.yaml`
6. Deploy a sample asset to the device: `kubectl apply -f examples/connector_get_adr_definitions_resources/rest-thermostat-asset-definition.yaml` 

### The sample Rust connector that uses the base connector to retrieve ADR definitions and send transformed data is currently under the `examples` folder of this crate.

#### Pre-requisites:
1. Have AIO Deployed with necessary features
1. Have an Azure Container Registry instance

To deploy follow these steps (from the root of the crate):
1. Create a binary release of the connector code: `cargo build --release --target-dir target --example base_connector_sample`
1. Build the docker container: `docker build -t baseconnector:latest -f examples/base_connector_sample_resources/Dockerfile .`
1. Tag your docker image `docker tag baseconnector <your ACR name>.azurecr.io/baseconnector`
1. Make sure you're logged into azure cli `az login`
1. Login to your ACR `az acr login --name <your ACR name>`
1. Upload to your ACR instance `docker push <your ACR name>.azurecr.io/baseconnector`
1. Apply the connector template: `kubectl apply -f examples/base_connector_sample_resources/connector_template.yaml`
1. Deploy a sample device: `kubectl apply -f examples/base_connector_sample_resources/thermostat-device-definition.yaml`
1. Deploy a sample asset to the device: `kubectl apply -f examples/base_connector_sample_resources/rest-thermostat-asset-definition.yaml` 

## Error Behaviors
Any error that is returned to the Connector Application will not be logged, it is the responsibility of the Connector Application to log them

### Errors that could trigger retries
All errors that are retried are logged when retried.

This is the retry strategy used: `const RETRY_STRATEGY: tokio_retry2::strategy::ExponentialFactorBackoff = tokio_retry2::strategy::ExponentialFactorBackoff::from_millis(500, 2.0);`

Any AIO Protocol Error is considered a "network error" and retriable. This may not be the case, but we currently don't provide enough information on the AIO Protocol Error to be able to distinguish the difference. Once we have that information, we will update handling to only retry actual network errors. Effort has been made to dive into the scenarios and ensure that any non-network errors are already validated against and not possible.

| Scenario that returns an error | Retry attempts | Retried Errors | Immediate Failure Errors | Logged? | Additional Behavior on failure | Notes |
|-|-|-|-|-|-|-|
|device/asset update observation|indefinite|Network Errors|Service Errors|Y|device/asset creation notification is dropped|It is not a problem to block other operations on these retries because they would also be affected by network issues.|
|device/asset update unobservation|indefinite|Network Errors|Service Errors|Y||This is a cleanup action, so if it fails, we just log it.|
|get device/asset definition, or get device/asset status|indefinite|Network Errors|Service Errors|Y|device/asset creation notification is dropped and device/asset update unobservation is called|It is not a problem to block other operations on these retries because they would also be affected by network issues.|
|reporting status/message schema to ADR|10|Network Errors|Service Errors|N|Error returned to application/caller||
|* Asset status is reported by the base connector|None (because underlying operation is already retried)|-|-|Y|||
|Message Schema `PUT` to Schema Registry|indefinite|Network Errors|Service Errors|N|Error returned to application/caller||
|Base Connector's MQTT Session ends|None|-|-|N|Error returned to application|This is currently fatal|
|Forwarding Data|None|-|-|N|Error returned to application|Retry should be handled by the Connector Application so that the appropriate timing of retrying this piece of data can be configured. Standard MQTT retries/guarantees are in place|



### Errors that couldn't trigger retries
| Scenario | Logged? | Additional Behavior on failure | Notes |
|-|-|-|-|
|Asset has an invalid default dataset destination|Y|Error will be reported on it's status to ADR (see * for error handling of this action)|-|
|Dataset update has an invalid dataset destination|Y|Error will be reported on it's status to ADR (see * for error handling of this action). An `UpdatedInvalid` notification will be provided to the application instead of an `Updated` notification so that it knows not to operate on the dataset until a new update is received.|-|
|New dataset has an invalid dataset destination|Y|Error will be reported on it's status to ADR (see * for error handling of this action). The Dataset will not be provided to the Connector Application since it cannot be used|-|
|Update is received for a Dataset, but the DatasetClient has been dropped|Y|Datset update will be dropped|-|
|Device endpoint create notification provides a device/endpoint name that returns a device with no inbound endpoint from the service|Y|Unobserve is called, and the create notification is dropped|This is really only possible if the device endpoint gets deleted between the time we receive the notification and the get device call is made, so losing this notification means it was out of date.|

### Setup
If Connector Artifacts contain invalid or incorrect values, setup of the BaseConnector will return an error indicating the reason.

### Fatal
- Creating a new file mount DeviceEndpointCreateObservation is fatal if there's an error. There's no way to recover from this other than restarting the connector. It causes a panic (TODO: we could propogate to the application?)
- There are other .expect()s/.unwrap()s in our code that technically can trigger a panic, but they should not be possible, so will not be defined here.