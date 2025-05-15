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
