# Instructions to deploy sample Rust connector

The sample Rust connector is currently under the `examples` folder of this crate.

To deploy follow these steps (from the root of the crate):

1. Create a binary release of the connector code: `cargo build --release --target-dir sample_connector --example deployed_azure_device_registry`
2. Build the docker container: `docker build -t deployedadrtestconnector:latest -f Dockerfile .`
3. Import into your kubernetes cluster with: `k3d image import deployedadrtestconnector:latest`
4. Apply the connector template: `kubectl apply -f ct.yaml`
5. Deploy a sample device: `kubectl apply -f my-thermostat-device.yaml`
6. Deploy a sample asset to the device: `kubectl apply -f rest-asset.yaml` 