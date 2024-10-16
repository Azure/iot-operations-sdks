# Tutorial: Build an event-driven app

In this tutorial, you deploy an application to the cluster. The application consumes simulated MQTT data from the MQTT broker, applies a windowing function, and then publishes the result back to MQTT broker. The published output shows how high volume data can be aggregated on the edge to reduce message frequency and size. The application is stateless, and uses the state store to cache values needed for the window calculations.

## What does it do?

The application consists of two workers (input and output) that together perform a sliding window calculation over the past 60 seconds.

The **InputWorker** performs the following steps:

1. Subscribes to the `sensor/data` topic and waits for incoming data
1. Fetches the historical list of data from the state store
1. Expunge data older than **60 seconds**
1. Appends the new data to the list
1. Pushes the updated list to the state store

The **OutputWorker** performs the following steps:

1. Every **10 seconds**, data is fetched from the state store
1. Calculations are performed on the data timestamped in the last **60 seconds**
1. The resulting windowed data is published to the `sensor/window_data` topic

## Prerequisites

1. Follow the [Getting started](/README.md#getting-started) guide to install Azure IoT Operations in Codespaces.

> [!NOTE]
> The guide assumes that the MQTT broker is running with SAT authentication on port 8884. The codespace environment already provides this configuration.

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

## Run the application on cluster

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
