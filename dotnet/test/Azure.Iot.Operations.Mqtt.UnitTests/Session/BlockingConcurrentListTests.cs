// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using Xunit;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class BlockingConcurrentListTests
    {
        [Fact]
        public async Task AddLastAsyncOverflow_DropOldest()
        {
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            BlockingConcurrentList list = new(2, MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage);

            var firstEnqueuedItem = new MqttApplicationMessage("someTopic") { CorrelationData = [1] };
            var secondEnqueuedItem = new MqttApplicationMessage("someTopic") { CorrelationData = [2] };
            var thirdEnqueuedItem = new MqttApplicationMessage("someTopic") { CorrelationData = [3] };
            
            await list.AddLastAsync(new QueuedPublishRequest(firstEnqueuedItem, new TaskCompletionSource<MqttClientPublishResult>()), cts.Token);
            await list.AddLastAsync(new QueuedPublishRequest(secondEnqueuedItem, new TaskCompletionSource<MqttClientPublishResult>()), cts.Token);
            await list.AddLastAsync(new QueuedPublishRequest(thirdEnqueuedItem, new TaskCompletionSource<MqttClientPublishResult>()), cts.Token);

            Assert.Equal(2, list.Count);

            QueuedRequest firstDequeuedItem = await list.PeekNextUnsentAsync(cts.Token);
            QueuedRequest secondDequeuedItem = await list.PeekNextUnsentAsync(cts.Token);

            Assert.True(firstDequeuedItem is QueuedPublishRequest);
            Assert.True(secondDequeuedItem is QueuedPublishRequest);
            QueuedPublishRequest firstDequeuedPublish = (QueuedPublishRequest)firstDequeuedItem;
            QueuedPublishRequest secondDequeuedPublish = (QueuedPublishRequest)secondDequeuedItem;
            Assert.Equal(secondEnqueuedItem.CorrelationData, firstDequeuedPublish.Request.CorrelationData);
            Assert.Equal(thirdEnqueuedItem.CorrelationData, secondDequeuedPublish.Request.CorrelationData);
        }

        [Fact]
        public async Task AddLastAsyncOverflow_DropNewest()
        {
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            BlockingConcurrentList list = new(2, MqttPendingMessagesOverflowStrategy.DropNewMessage);

            var firstEnqueuedItem = new MqttApplicationMessage("someTopic") { CorrelationData = [1] };
            var secondEnqueuedItem = new MqttApplicationMessage("someTopic") { CorrelationData = [2] };
            var thirdEnqueuedItem = new MqttApplicationMessage("someTopic") { CorrelationData = [3] };

            await list.AddLastAsync(new QueuedPublishRequest(firstEnqueuedItem, new TaskCompletionSource<MqttClientPublishResult>()), cts.Token);
            await list.AddLastAsync(new QueuedPublishRequest(secondEnqueuedItem, new TaskCompletionSource<MqttClientPublishResult>()), cts.Token);
            await list.AddLastAsync(new QueuedPublishRequest(thirdEnqueuedItem, new TaskCompletionSource<MqttClientPublishResult>()), cts.Token);

            Assert.Equal(2, list.Count);

            QueuedRequest firstDequeuedItem = await list.PeekNextUnsentAsync(cts.Token);
            QueuedRequest secondDequeuedItem = await list.PeekNextUnsentAsync(cts.Token);

            Assert.True(firstDequeuedItem is QueuedPublishRequest);
            Assert.True(secondDequeuedItem is QueuedPublishRequest);
            QueuedPublishRequest firstDequeuedPublish = (QueuedPublishRequest)firstDequeuedItem;
            QueuedPublishRequest secondDequeuedPublish = (QueuedPublishRequest)secondDequeuedItem;
            Assert.Equal(firstEnqueuedItem.CorrelationData, firstDequeuedPublish.Request.CorrelationData);
            Assert.Equal(secondEnqueuedItem.CorrelationData, secondDequeuedPublish.Request.CorrelationData);
        }
    }
}
