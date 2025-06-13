# Instructions to deploy sample Rust connector

### The sample Rust connector that retrieves ADR definitions is currently under the `examples` folder of this crate.

To deploy follow these steps (from the root of the crate):

1. Create a binary release of the connector code: `cargo build --release --target-dir target_connector_get_adr_definitions --example connector_get_adr_definitions`
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
1. Create a binary release of the connector code: `cargo build --release --target-dir target_base_connector_sample --example base_connector_sample`
1. Build the docker container: `docker build -t baseconnector:latest -f examples/base_connector_sample_resources/Dockerfile .`
1. Tag your docker image `docker tag baseconnector <your ACR name>.azurecr.io/baseconnector`
1. Make sure you're logged into azure cli `az login`
1. Login to your ACR `az acr login --name <your ACR name>`
1. Upload to your ACR instance `docker push <your ACR name>.azurecr.io/baseconnector`
1. Apply the connector template: `kubectl apply -f examples/base_connector_sample_resources/connector_template.yaml`
1. Deploy a sample device: `kubectl apply -f examples/base_connector_sample_resources/thermostat-device-definition.yaml`
1. Deploy a sample asset to the device: `kubectl apply -f examples/base_connector_sample_resources/rest-thermostat-asset-definition.yaml` 

## Error Behaviors
### Retried
All errors that are retried are logged when retried.
This is the retry strategy used: `const RETRY_STRATEGY: tokio_retry2::strategy::ExponentialFactorBackoff = tokio_retry2::strategy::ExponentialFactorBackoff::from_millis(500, 2.0);`
Any AIO Protocol Error is considered a "network error" and retriable. This may not be the case, but we currently don't provide enough information on the AIO Protocol Error to be able to distinguish the difference. Once we have that information, we will update handling to only retry actual network errors. Effort has been made to dive into the scenarios and ensure that any non-network errors are already validated against and not possible.

#### Setup
These errors will retry indefinitely with exponential backoff, hoping new connector artifacts will fix the error
- errors parsing the Connector Artifacts
- errors creating the MQTT Session (including creating needed settings)
- errors creating azure device registry client
- errors creating state store client
- note: creating schema registry client doesn't return any errors


- If the (device/asset update observation, device/asset update unobservation, get device/asset definition, or get device/asset status) request fails because of network errors, it retries indefinitely with exponential backoff and jitter. Service Errors fail immediately as they are not retriable. It is not a problem to block other operations on these retries because they would also be affected by network issues.
- If reporting status/message schema to ADR fails because of a network error, it will retry up to 10 times with exponential backoff and jitter. If it still does not succeed or an error is from the service, the error will be returned to the application.
- If putting a message schema to the Schema Registry Service fails because of a network error, it retries indefinitely with exponential backoff and jitter. If an error is returned from the service, the error will be returned to the application without any retries.




### Only Logged
- if the device/asset update observation retries fail, then the device/asset creation notification is dropped
- if the (get device/asset definition, or get device/asset status) retries fail, then device/asset update unobservation is called (failure just logs) and the device/asset creation notification is dropped.
- If the device endpoint create notification provides a device/endpoint name that returns a device with no inbound endpoint from the service, this is logged, unobserve is called, and the create notification is dropped. This is really only possible if the device endpoint gets deleted between the time we receive the notification and the get device call is made, so losing this notification means it was out of date.
- * If an asset status is reported by the base connector and it fails (after retries/non-retriable failure), this error is logged
- If an asset has an invalid default destination, this will be logged and the error will be reported to it's status on ADR. (see * for error handling of this action)
- If a new dataset has an invalid destination and no default destination, it will be logged and the error will be reported on it's status to ADR (see * for error handling of this action). The Dataset will not be provided to the Connector Application since it cannot be used.
- If an update is received for a Dataset, but the DatasetClient has been dropped, the update will be logged and ignored.


### Returned to Connector Application
These errors will not be logged, it is the responsibility of the Connector Application to log them
- Base Connector's MQTT Session ending returns the error to the connector application. This is currently fatal
- If there are any errors when trying to forward data, this is returned to the Connector Application. Retry should be handled by the Connector Application so that the appropriate timing of retrying this piece of data can be configured. Standard MQTT retries/guarantees are in place
- If a dataset update has an invalid destination and no default destination, it will be logged and the error will be reported on it's status to ADR (see * for error handling of this action). An UpdatedInvalid notification will be provided to the application instead of an Updated notification so that it knows not to operate on the dataset until a new update is received.

### Fatal
- Creating a new file mount DeviceEndpointCreateObservation is fatal if there's an error. There's no way to recover from this other than restarting the connector. It causes a panic (TODO: we could propogate to the application?)
- There are other .expect()s/.unwrap()s in our code that technically can trigger a panic, but they should not be possible, so will not be defined here.
