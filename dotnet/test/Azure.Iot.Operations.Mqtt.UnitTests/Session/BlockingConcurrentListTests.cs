// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

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

        [Fact(Timeout = 10000)]
        public async Task AddLastAsync_WaitingRequestsCompleteWhenDisposedDuringClosure()
        {
            using BlockingConcurrentList list = new(2, MqttPendingMessagesOverflowStrategy.DropNewMessage);
            using ManualResetEventSlim closeRequestEntered = new();
            using ManualResetEventSlim releaseCloseRequest = new();
            using ManualResetEventSlim addRequestEntered = new();
            using ManualResetEventSlim releaseAddRequests = new();
            List<Exception> addRequestExceptions = new();

            await list.AddLastAsync(
                new TestQueuedRequest(_ =>
                {
                    closeRequestEntered.Set();
                    releaseCloseRequest.Wait();
                }),
                CancellationToken.None);

            Task closeTask = Task.Run(() => list.CloseAsync(new ObjectDisposedException(nameof(list)), CancellationToken.None));
            Assert.True(closeRequestEntered.Wait(TimeSpan.FromSeconds(5)));

            Task[] addTasks = Enumerable.Range(0, 3)
                .Select(_ => list.AddLastAsync(
                    new TestQueuedRequest(exception =>
                    {
                        addRequestExceptions.Add(exception);
                        addRequestEntered.Set();
                        releaseAddRequests.Wait();
                    }),
                    CancellationToken.None))
                .ToArray();

            releaseCloseRequest.Set();
            await closeTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(addRequestEntered.Wait(TimeSpan.FromSeconds(5)));

            list.Dispose();
            releaseAddRequests.Set();

            await Task.WhenAll(addTasks).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(3, addRequestExceptions.Count);
            Assert.All(addRequestExceptions, exception => Assert.IsType<ObjectDisposedException>(exception));
        }

        private sealed class TestQueuedRequest : QueuedRequest
        {
            private readonly Action<Exception> _onException;

            public TestQueuedRequest(Action<Exception> onException)
            {
                _onException = onException;
            }

            internal override void OnException(Exception reason) => _onException(reason);
        }
    }
}
