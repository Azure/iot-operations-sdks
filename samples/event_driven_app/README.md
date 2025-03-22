# Tutorial: Build an event-driven app

In this tutorial, you deploy an application to the cluster. The application consumes simulated MQTT data from the MQTT broker, applies a windowing function, and then publishes the result back to MQTT broker. The published output shows how high volume data can be aggregated on the edge to reduce message frequency and size. The application is stateless, and uses the state store to cache values needed for the window calculations.

## What does it do?

The application consists of two workers (input and output) that together perform a sliding window calculation over the past 60 seconds.

The **Input Client** performs the following steps:

1. Subscribes to the `sensor/data` topic and waits for incoming data
1. Fetches the historical list of data from the state store
1. Expunge data older than **60 seconds**
1. Appends the new data to the list
1. Pushes the updated list to the state store

The **Output Client** performs the following steps:

1. Every **10 seconds**, data is fetched from the state store
1. Calculations are performed on the data timestamped in the last **60 seconds**
1. The resulting windowed data is published to the `sensor/window_data` topic

## Tutorial languages

This tutorial is available in the following languages:

 * [.NET](/dotnet/samples/applications/EventDrivenApp)
 * [Rust](/rust/sample_applications/event_driven_app)
 * [Go](/go/samples/application/eventdrivenapp)

## Prerequisites

1. Follow the [Setup documentation](/doc/setup.md) to setup your environment and install Azure IoT Operations

1. Open a shell in the [Azure IoT Operations SDKS repository](https://github.com/azure/iot-operations-sdks/) root.

1. Source the [.env](/.env) to export variables used by the samples to connect to MQTT broker:

    ```bash
    source .env
    ```

> [!NOTE]
> The guide assumes that the MQTT broker is running with SAT authentication on port 8884. The [setup](/doc/setup.md) instructions will configure the MQTT broker with this configuration.

## Build the application

Build the application within your development environment following the instructions for the language of your choice:

1. Build the application:

    <details>
    <summary>.NET</summary>

    ```bash
    dotnet build dotnet/samples/applications/EventDrivenApp
    ```

    </details>

    <details>
    <summary>Rust</summary>

    Rust contains separate applications for the input client and the output client.

    ```bash
    cd rust
    cargo build -p input_client -p output_client
    ```

    </details>

    <details>
    <summary>Go</summary>

    ```bash
    go build -C go/samples/application/eventdrivenapp
    ```

    </details>

> [!TIP]
> You can run the application directly from your development environment if you are using the standard [setup](/doc/setup.md) as the MQTT broker will be available externally from the cluster.

## Deploy the application

The application will be deployed to the cluster by building a container and applying the `app.yaml`:

1. Build the application container and import to your cluster:

    <details>
    <summary>.NET</summary>

    ```bash
    cd dotnet/samples/applications/EventDrivenApp
    docker build -t event-driven-app .
    k3d image import event-driven-app
    ```

    </details>

    <details>
    <summary>Rust</summary>

    ```bash
    cd rust
    docker build -f sample_applications/event_driven_app/Dockerfile -t event-driven-app .
    k3d image import event-driven-app
    ```

    </details>

    <details>
    <summary>Go</summary>

    ```bash
    cd go/samples/application/eventdrivenapp
    docker build -t event-driven-app .
    k3d image import event-driven-app
    ```

    </details>

1. Deploy the application to the cluster:

    <details>
    <summary>.NET</summary>

    ```bash
    kubectl apply -f dotnet/samples/applications/EventDrivenApp/app.yaml
    ```

    </details>

    <details>
    <summary>Rust</summary>

    ```bash
    kubectl apply -f rust/sample_applications/event_driven_app/app.yaml
    ```

    </details>

    <details>
    <summary>Go</summary>

    ```bash
    kubectl apply -f go/samples/application/eventdrivenapp/app.yaml
    ```

    </details>

1. Confirm that the application running by getting the pod status:

    ```bash
    kubectl get pods -l app=event-driven-app -n azure-iot-operations
    ```

    Output:

    ```output
    NAME                   READY   STATUS              RESTARTS   AGE
    event-driven-app-xxx   1/1     Running             0          10s
    ```

## Deploy the simulator

Create test data by deploying a simulator. It emulates a sensor by sending sample temperature, vibration, and pressure readings to the MQTT broker on the `sensor/data` topic every 10 seconds.

1. Deploy the simulator to the cluster:

    ```bash
    kubectl apply -f samples/EventDrivenApp/simulator.yaml
    ```

1. Confirm the simulator is running correctly by observing the published messages:

    ```bash
    kubectl logs -f -l app=event-driven-app-simulator -n azure-iot-operations
    ```

    Output:

    ```output
    fetch https://dl-cdn.alpinelinux.org/alpine/v3.20/main/x86_64/APKINDEX.tar.gz
    fetch https://dl-cdn.alpinelinux.org/alpine/v3.20/community/x86_64/APKINDEX.tar.gz
    ...
    Starting simulator
    Publishing 5 messages
    Publishing 10 messages
    ```

## Verify the application output

1. Export a variable for the location of the session information (MQTT trust bundle and SAT)

    ```bash
    export SESSION=$(git rev-parse --show-toplevel)/.session
    ```

1. Subscribe to the `sensor/data` topic to observe the simulator is outputting data:

    ```bash
    mosquitto_sub -L mqtts://localhost:8884/sensor/data --cafile $SESSION/broker-ca.crt -D CONNECT authentication-method K8S-SAT -D CONNECT authentication-data $(cat $SESSION/token.txt)
    ```

1. Subscribe to the `sensor/window_data` topic to observe the published output from this application:

    ```bash
    mosquitto_sub -L mqtts://localhost:8884/sensor/window_data --cafile $SESSION/broker-ca.crt -D CONNECT authentication-method K8S-SAT -D CONNECT authentication-data $(cat $SESSION/token.txt)
    ```

1. Verify the application is outputting a sliding windows calculation for the various simulated sensors every **10 seconds**:

    ```json
    {
        "Timestamp": "2024-10-02T22:43:12.4756119Z",
        "WindowSize": 60,
        "Temperature": {
            "Min": 553.024,
            "Max": 598.907,
            "Mean": 576.4647857142858,
            "Median": 577.4905,
            "Count": 20
        },
        "Pressure": {
            "Min": 290.605,
            "Max": 299.781,
            "Mean": 295.521,
            "Median": 295.648,
            "Count": 20
        },
        "Vibration": {
            "Min": 0.00124192,
            "Max": 0.00491257,
            "Mean": 0.0031171810714285715,
            "Median": 0.003199235,
            "Count": 20
        }
    }
    ```

## Troubleshooting

### Application fails to authenticate with MQTT broker

1. *Local execution* - Make sure to `source` the .env file in the root to export the requirement environment variables.
1. *Local execution* - The SAT auth token can expire, download a new token by refreshing credentials:

    ```bash
    ./tools/deployment/update-credentials.sh
    ```

1. *On cluster* - check the Deployment yaml to make sure that the required volume mounts are present.

## Next steps

Now that you have completed the event driven app tutorial, try running it again in another language, or make customizations by adding additional calculations or sensors.
