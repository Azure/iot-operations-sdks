# Tutorial: Build an event-driven app

In this tutorial, you deploy an application to the cluster. The application consumes simulated MQTT data published to MQTT broker, applies a windowing function, and then publishes the result back to MQTT broker. The published output shows how high volume data can be aggregated on the edge to reduce message frequency and size. The application is stateless, and uses the state store to cache values needed for the window calculations.

The application performs the following steps:

1. Subscribes to the `sensor/data` topic for sensor data.
1. When data is receiving on the topic, it's published to the state store.
2. Every **10 seconds**, it fetches the data from the state store and performs a number of windowing caculations for the last **30 seconds** of data.
3. Data older than **30 seconds** is removed from the state store.
4. The result is published to the `sensor/window_data` topic in JSON format.

## Prerequisites

* Follow the [Getting started](/#getting-started) guide to install Azure IoT Operations in Codespaces.

## Build the application

1. Build the application

    ```bash
    dotnet build
    ```

## Run the application locally

The application can be run locally, by downloading a SAT, and the MQTT broker cert to the local development environment.

1. Regenerate the SAT and server cert if needed:

    ```bash
    ../../../tools/deployment/update-credentials.sh
    ```

1. Run the application:

    ```bash
    dotnet run
    ```

## Deploy the application to the cluster

The application can be deployed to the cluster using the `app.yml` file located in the current directory:

1. Build the container:

    ```bash
    dotnet publish --os linux --arch x64 /t:PublishContainer
    ```

1. Upload the container to the local k3d cluster:

    ```bash
    k3d image import event-driven-app
    ```

1. Deploy the application:

    ```bash
    kubectl apply -f app.yml
    ```

1. Confirm that the application deployed successfully. The pod should report all containers are ready after a short interval:

    ```bash
    kubectl get pods event-driven-app -n azure-iot-operations
    ```

    Output:

    ```output
    NAME               READY   STATUS              RESTARTS   AGE
    event-driven-app   1/1     Running             0          10s
    ```

## Deploy the simulator

Simulate test data by deploying a simulator pod. It simulates a sensor by sending sample temperature, vibration, and pressure readings periodically to the MQTT broker on the `sensor/data` topic.

1. Deploy the simulator:

    ```bash
    kubectl apply -f simulator.yml
    ```

1. Confirm the simulator is running correctly:

    ```bash
    kubectl logs mqtt-simulator -n azure-iot-operations -f
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

1. View the simulator output:

    ```bash
    export SESSION=$(git rev-parse --show-toplevel)/.session
    mosquitto_sub -L mqtts://localhost:8884/sensor/data -V mqttv311 --cafile $SESSION/broker-ca.crt -u K8S-SAT -P $(cat $SESSION/token.txt)
    ```

## Verify the application output

1. Subscribe to the `sensor/window_data` topic to observe the published output from the Dapr application:

    ```bash
    mosquitto_sub -L mqtt://localhost/sensor/window_data
    ```

1. Verify the application is outputting a sliding windows calculation for the various sensors every 10 seconds:

    ```json
    {
        "timestamp": "2023-11-16T21:59:53.939690+00:00",
        "window_size": 30,
        "temperature": {
            "min": 553.024,
            "max": 598.907,
            "mean": 576.4647857142858,
            "median": 577.4905,
            "75_per": 585.96125,
            "count": 28
        },
        "pressure": {
            "min": 290.605,
            "max": 299.781,
            "mean": 295.521,
            "median": 295.648,
            "75_per": 297.64050000000003,
            "count": 28
        },
        "vibration": {
            "min": 0.00124192,
            "max": 0.00491257,
            "mean": 0.0031171810714285715,
            "median": 0.003199235,
            "75_per": 0.0038769150000000003,
            "count": 28
        }
    }
    ```

## How the application was built

1. Create the application from the [worker template](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-sdk-templates#web-others):

    ```bash
    dotnet new worker
    ```

1. Add the Azure IoT Operations SDK feed and install the packages:

    ```bash
    dotnet nuget add source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json -n AzureIoTOperations
    dotnet add package Azure.Iot.Operations.Mqtt --prerelease
    dotnet add package Azure.Iot.Operations.Protocol --prerelease
    ```

1. Fill out the Worker.cs with the application logic.

