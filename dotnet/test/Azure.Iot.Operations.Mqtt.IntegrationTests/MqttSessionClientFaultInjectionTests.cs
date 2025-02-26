// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.IntegrationTests;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Protocol.Session.IntegrationTests
{
    [Trait("Category", "FaultInjection")]
    public class MqttSessionClientFaultInjectionTests
    {

        [Fact]
        public async Task TestSessionClientHandlesFailedConnackDuringConnect()
        {
            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.ServerBusy;
            List<MqttUserProperty> ConnectUserProperties =
            [
                new MqttUserProperty(FaultInjectionTestConstants.rejectConnectFaultName, "" + ((int)expectedReason)),
                new MqttUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString()),
            ];

            // The first connection attempt should fail, but the session client's retry policy should make it
            // connect again. The broker should accept the second connection attempt.
            await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv(ConnectUserProperties);
        } 
        
        [Fact]
        public async Task TestSessionClientHandlesDisconnectWhileIdle()
        {   
            await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv();

            TaskCompletionSource<MqttClientDisconnectedEventArgs> faultWasInjectedTcs = new();
            sessionClient.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs.TrySetResult(args);
                return Task.CompletedTask;
            };

            TaskCompletionSource<MqttClientDisconnectedEventArgs> sessionLostTcs = new();
            sessionClient.SessionLostAsync += (args) =>
            {
                sessionLostTcs.TrySetResult(args);
                return Task.CompletedTask;
            };

            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.ServerBusy;
            byte[] expectedPayload = Guid.NewGuid().ToByteArray();

            // This fault injection publish will be ack'd as normal, but will tell the broker
            // to kill the connection 1 second after receiving the publish
            MqttApplicationMessage faultMessage = new MqttApplicationMessage(Guid.NewGuid().ToString())
            {
                PayloadSegment = expectedPayload,
            };

            faultMessage.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            faultMessage.AddUserProperty(FaultInjectionTestConstants.disconnectFaultDelayName, "1");
            faultMessage.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());

            var result = await sessionClient.PublishAsync(faultMessage).WaitAsync(TimeSpan.FromMinutes(1));
            Assert.True(result.IsSuccess);

            // Wait until the fault injection happens or until a timeout
            var faultDetails = await faultWasInjectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(expectedReason, faultDetails.Reason);

            // The session client should handle the fault and reconnect either prior to this publish or after this publish
            // is initiated. In either case, the publish should be sent successfully
            await sessionClient.PublishAsync(new MqttApplicationMessage("someNormalPublish"));
        } 
  
        [Fact]
        public async Task TestSessionClientHandlesDisconnectDuringPublish()
        {
            await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv();

            TaskCompletionSource<MqttClientDisconnectedEventArgs> faultWasInjectedTcs = new();
            sessionClient.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs.TrySetResult(args);
                return Task.CompletedTask;
            };

            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.AdministrativeAction;
            byte[] expectedPayload = Guid.NewGuid().ToByteArray();

            MqttApplicationMessage faultMessage = new MqttApplicationMessage(Guid.NewGuid().ToString())
            {
                PayloadSegment = expectedPayload,
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
            };

            faultMessage.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            faultMessage.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());

            var result = await sessionClient.PublishAsync(faultMessage).WaitAsync(TimeSpan.FromMinutes(1));

            Assert.Equal(expectedReason, (await faultWasInjectedTcs.Task.WaitAsync(TimeSpan.FromMinutes(1))).Reason);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task TestSessionClientHandlesDisconnectDuringSubscribe()
        {
            await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv();

            TaskCompletionSource<MqttClientDisconnectedEventArgs> faultWasInjectedTcs = new();
            sessionClient.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs.TrySetResult(args);
                return Task.CompletedTask;
            };

            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.AdministrativeAction;
            string expectedTopic = "myTopic/" + Guid.NewGuid().ToString();
            var subscribeOptions = new MqttClientSubscribeOptions(expectedTopic, MqttQualityOfServiceLevel.AtLeastOnce);
            subscribeOptions.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            subscribeOptions.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());

            MqttClientSubscribeResult subscribeResult = await sessionClient.SubscribeAsync(subscribeOptions).WaitAsync(TimeSpan.FromMinutes(1));

            Assert.Equal(expectedReason, (await faultWasInjectedTcs.Task.WaitAsync(TimeSpan.FromMinutes(1))).Reason);
            Assert.Single(subscribeResult.Items);
        }

        [Fact]
        public async Task TestSessionClientHandlesDisconnectDuringUnsubscribe()
        {
            await using MqttSessionClient sessionClient = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv();

            TaskCompletionSource<MqttClientDisconnectedEventArgs> faultWasInjectedTcs = new();
            sessionClient.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs.TrySetResult(args);
                return Task.CompletedTask;
            };

            string expectedTopic = "myTopic/" + Guid.NewGuid().ToString();
            await sessionClient.SubscribeAsync(new MqttClientSubscribeOptions(expectedTopic));
                
            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.ConnectionRateExceeded;
            var unsubscribeOptions = new MqttClientUnsubscribeOptions(expectedTopic);
            unsubscribeOptions.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            unsubscribeOptions.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());
            MqttClientUnsubscribeResult unsubscribeResult =
                await sessionClient.UnsubscribeAsync(unsubscribeOptions).WaitAsync(TimeSpan.FromMinutes(1));

            Assert.Equal(expectedReason, (await faultWasInjectedTcs.Task.WaitAsync(TimeSpan.FromMinutes(1))).Reason);
            Assert.Single(unsubscribeResult.Items);
        }
    }
}
