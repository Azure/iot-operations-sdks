package com.microsoft.azure.sdk.iot;

import com.hivemq.client.mqtt.MqttClient;
import com.hivemq.client.mqtt.datatypes.MqttQos;
import com.hivemq.client.mqtt.mqtt5.Mqtt5AsyncClient;
import com.hivemq.client.mqtt.mqtt5.Mqtt5BlockingClient;
import com.hivemq.client.mqtt.mqtt5.Mqtt5Client;
import com.hivemq.client.mqtt.mqtt5.Mqtt5RxClient;
import com.hivemq.client.mqtt.mqtt5.message.connect.connack.Mqtt5ConnAck;
import com.hivemq.client.mqtt.mqtt5.message.publish.Mqtt5Publish;
import com.hivemq.client.mqtt.mqtt5.message.subscribe.Mqtt5Subscribe;
import com.hivemq.client.mqtt.mqtt5.message.subscribe.Mqtt5Subscription;
import com.hivemq.client.mqtt.mqtt5.message.subscribe.Mqtt5SubscriptionBuilder;

import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;

public class Main {
    public static void main(String[] args) throws InterruptedException {
        Mqtt5Client client1 = MqttClient.builder()
                .useMqttVersion5()
                .identifier(UUID.randomUUID().toString()) // Unique client ID
                .serverHost("localhost") // HiveMQ public broker
                .serverPort(1884) // Default MQTT port
                .build();

        Mqtt5Client client2 = MqttClient.builder()
                .useMqttVersion5()
                .identifier(UUID.randomUUID().toString()) // Unique client ID
                .serverHost("localhost") // HiveMQ public broker
                .serverPort(1884) // Default MQTT port
                .build();

        Mqtt5RxClient asyncClient1 = client1.toRx();
        Mqtt5RxClient asyncClient2 = client2.toRx();

        System.out.println("Starting...");
        asyncClient1.toBlocking().connect();
        asyncClient2.toBlocking().connect();
        System.out.println("Connected!");

        Mqtt5Publish requestPublish = Mqtt5Publish.builder()
                .topic("timtay/requestTopic")
                .qos(MqttQos.AT_LEAST_ONCE)
                .build();

        Mqtt5Publish responsePublish = Mqtt5Publish.builder()
                .topic("timtay/responseTopic")
                .qos(MqttQos.AT_LEAST_ONCE)
                .build();

        asyncClient1.subscribePublishesWith()
                .topicFilter("timtay/responseTopic")
                .applySubscribe();

        Mqtt5Subscription subscribe1 = Mqtt5Subscription.builder()
                        .topicFilter("timtay/responseTopic")
                        .qos(MqttQos.AT_LEAST_ONCE)
                        .build();

        Mqtt5Subscription subscribe2 = Mqtt5Subscription.builder()
                .topicFilter("timtay/requestTopic")
                .qos(MqttQos.AT_LEAST_ONCE)
                .build();

        final AtomicInteger onResponseReceived = new AtomicInteger(0);

        asyncClient1.toAsync()
            .subscribeWith()
            .addSubscription(subscribe1)
            .callback(receivedPublish ->
            {
                onResponseReceived.addAndGet(1);
            })
            .send();

        asyncClient2.toAsync()
                .subscribeWith()
                .addSubscription(subscribe2)
                .callback(receivedPublish ->
                {
                    asyncClient2.toBlocking().publish(responsePublish);
                })
                .send();

        int expectedInt = 0;
        while (true)
        {
            Thread.sleep(1000);

            long startTime = System.currentTimeMillis();
            asyncClient1.toBlocking().publish(requestPublish);

            while (onResponseReceived.get() == expectedInt)
            {
                Thread.sleep(5);
            }
            long finishTime = System.currentTimeMillis();

            System.out.println("Diff: " + (finishTime - startTime));
            expectedInt++;
        }
    }
}