using Azure.Iot.Operations.Mqtt;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class BlockingConcurrentDelayableQueueTests
    {
        private class TestQueueObject : IDelayableQueueItem
        {
            private bool _ready;

            public TestQueueObject(bool ready = false) 
            { 
                _ready = ready;
            }

            public bool IsReady()
            {
                return _ready;
            }

            public void MarkAsReady()
            {
                _ready = true;
            }
        }

        [Fact]
        public void TestDequeueWhenReady()
        {
            var queueItem = new TestQueueObject(true);
            BlockingConcurrentDelayableQueue<TestQueueObject> testQueue = new();
            testQueue.Enqueue(queueItem);
            Assert.Equal(1, testQueue.Count);
            TestQueueObject dequeuedItem = testQueue.Dequeue();
            Assert.Equal(0, testQueue.Count);
            Assert.Equal(queueItem, dequeuedItem);
        }

        [Fact]
        public void TestDequeueWhenNotReady()
        {
            var queueItem = new TestQueueObject(false);
            BlockingConcurrentDelayableQueue<TestQueueObject> testQueue = new();
            testQueue.Enqueue(queueItem);

            using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            Assert.Throws<OperationCanceledException>(() => testQueue.Dequeue(cts2.Token));

            Assert.Equal(1, testQueue.Count);
            
            queueItem.MarkAsReady();

            // Now that the queue item is ready to be dequeued, both the peek and dequeue calls should return immediately
            using var cts3 = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            TestQueueObject dequeuedItem = testQueue.Dequeue(cts3.Token);
            Assert.Equal(0, testQueue.Count);
            Assert.Equal(queueItem, dequeuedItem);
        }

        [Fact(Skip = "Not very reliable. Need to rework this test.")]
        public void TestWakingUpDequeueWithSignal()
        {
            var queueItem = new TestQueueObject(false);
            BlockingConcurrentDelayableQueue<TestQueueObject> testQueue = new();
            testQueue.Enqueue(queueItem);

            // After a second, mark the item as ready to dequeue and signal the queue to wakeup
            // the peek operation that should be waiting.
            new Task(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                queueItem.MarkAsReady();
                testQueue.Signal();
            }).Start();

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            TestQueueObject peekedItem = testQueue.Dequeue(cts1.Token);

            Assert.Equal(queueItem, peekedItem);
            Assert.Equal(0, testQueue.Count);
        }

        [Fact]
        public void SignallingQueueDoesNotAffectReadyStatusOfQueuedItem()
        {
            var queueItem = new TestQueueObject(false);
            BlockingConcurrentDelayableQueue<TestQueueObject> testQueue = new();
            testQueue.Enqueue(queueItem);

            // After a second, mark the item as ready to dequeue and signal the queue to wakeup
            // the peek operation that should be waiting.
            new Task(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300));
                testQueue.Signal();
            }).Start();

            // Even though the queue gets signalled, the peek operation should timeout because
            // the item in the queue is still not marked as ready.
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            Assert.Throws<OperationCanceledException>(() => testQueue.Dequeue(cts1.Token));
        }

        [Fact]
        public void QueueOrderNotImpactedByItemReadyOrder()
        {
            var queueItem1 = new TestQueueObject(false);
            var queueItem2 = new TestQueueObject(true);
            BlockingConcurrentDelayableQueue<TestQueueObject> testQueue = new();
            testQueue.Enqueue(queueItem1);
            testQueue.Enqueue(queueItem2);

            // Even though one item in the queue is ready to be dequeued, the first item is not so this call
            // should timeout.
            using var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            Assert.Throws<OperationCanceledException>(() => testQueue.Dequeue(cts1.Token));

            queueItem1.MarkAsReady();
            
            // Now that the both items in the queue are ready to be dequeued, the dequeue operations should
            // succeed
            using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            TestQueueObject dequeuedItem1 = testQueue.Dequeue(cts2.Token);
            TestQueueObject dequeuedItem2 = testQueue.Dequeue(cts2.Token);

            Assert.Equal(queueItem1, dequeuedItem1);
            Assert.Equal(queueItem2, dequeuedItem2);
        }
    }
}
