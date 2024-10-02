# Tutorial: Build an event-driven app

In this tutorial, you deploy an application to the cluster. The application consumes simulated MQTT data from the MQTT broker, applies a windowing function, and then publishes the result back to MQTT broker. The published output shows how high volume data can be aggregated on the edge to reduce message frequency and size. The application is stateless, and uses the state store to cache values needed for the window calculations.

## The application structure

The application consists of two workers (input and output) that together perform a sliding window calculation over the past 60 seconds.

The **InputWorker** performs the following steps:

1. Subscribes to the `sensor/data` topic and waits for incoming data
1. Fetches the historical list of data from the state store
1. Expunge data older than **60 seconds**
1. Appends the new data to the list
1. Pushes the updated list to the state store

The OutputWorker performs the following steps:

1. Every **10 seconds**, data is fetched from the state store
1. Calculations are performed on the data timestamped in the last **60 seconds**
1. The resulting windowed data is published to the `sensor/window_data` topic

## Prerequisites

1. Follow the [Getting started](/README.md#getting-started) guide to install Azure IoT Operations in Codespaces.

> [!NOTE]
> The guide assumes that the MQTT broker is running with SAT authentication on port 8884. The Codespaces environment is already configured in this way.

## Deploy the simulator

Create test data by deploying a simulator. It emulates a sensor by sending sample temperature, vibration, and pressure readings to the MQTT broker on the `sensor/data` topic every 10 seconds.

1. Deploy the simulator:

    ```bash
    kubectl apply -f yaml/simulator.yml
    ```

1. Confirm the simulator is running correctly by subscribing to its publishes:

    ```bash
    kubectl logs -l app=mqtt-simulator -n azure-iot-operations -f
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

## Run the application locally

The application can be run locally by fetching a SAT and broker cert from the cluster. Deploying locally simplifies application debugging.

1. Pull the SAT and MQTT broker cert from the cluster:

    ```bash
    ../../../tools/deployment/update-credentials.sh
    ```

1. Run the application:

    ```bash
    dotnet run
    ```

## Deploy the application to the cluster

The application can also be deployed to the cluster by building a container and applying the `app.yml` file:

1. Build the container and upload it the the local k3d cluster:

    ```bash
    dotnet publish --os linux --arch x64 /t:PublishContainer
    k3d image import event-driven-app
    ```

1. Deploy the application:

    ```bash
    kubectl apply -f yaml/app.yml
    ```

1. Confirm that the application deployed successfully. The pod should report all containers are ready after a short interval:

    ```bash
    kubectl get pods -l app=event-driven-app -n azure-iot-operations
    ```

    Output:

    ```output
    NAME                   READY   STATUS              RESTARTS   AGE
    event-driven-app-xxx   1/1     Running             0          10s
    ```

## Verify the application output

1. Subscribe to the `sensor/window_data` topic to observe the published output from this application:

    ```bash
    export SESSION=$(git rev-parse --show-toplevel)/.session
    mosquitto_sub -L mqtts://localhost:8884/sensor/window_data --cafile $SESSION/broker-ca.crt -D CONNECT authentication-method K8S-SAT -D CONNECT authentication-data $(cat $SESSION/token.txt)
    ```

1. Verify the application is outputting a sliding windows calculation for the various simulated sensors every 10 seconds:

    ```json
    {
        "timestamp": "2024-10-02T22:43:12.4756119Z",
        "window_size": 60,
        "temperature": {
            "min": 553.024,
            "max": 598.907,
            "mean": 576.4647857142858,
            "median": 577.4905,
            "count": 20
        },
        "pressure": {
            "min": 290.605,
            "max": 299.781,
            "mean": 295.521,
            "median": 295.648,
            "count": 20
        },
        "vibration": {
            "min": 0.00124192,
            "max": 0.00491257,
            "mean": 0.0031171810714285715,
            "median": 0.003199235,
            "count": 20
        }
    }
    ```
