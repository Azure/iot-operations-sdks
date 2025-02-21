// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    using System.Buffers;
    using System.Text;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Protocol.RPC;

    public class CommandResponseCacheUnitTests
    {
        private class TestCommandResponseCache : CommandResponseCache
        {
            public double? CachingBenefit { get; set; }

            public override double CostWeightedBenefit(ReadOnlySequence<byte> requestPayload, MqttApplicationMessage responseMessage, TimeSpan executionDuration)
            {
                return CachingBenefit != null ? (double)CachingBenefit : base.CostWeightedBenefit(requestPayload, responseMessage, executionDuration);
            }
        }

        private const string MqttTopic1 = "some/topic";
        private const string MqttTopic2 = "some/other/topic";

        private const string CommandName1 = "Command1";
        private const string CommandName2 = "Command2";

        private const string InvokerId1 = "Invoker1";
        private const string InvokerId2 = "Invoker2";

        private readonly TimeSpan _temporalTestSoonDuration = TimeSpan.FromSeconds(0.25);
        private readonly TimeSpan _temporalTestQuiescenceDelay = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _temporalTestLoopDelay = TimeSpan.FromSeconds(0.1);
        private readonly int _temporalTestLoopLimit = 10;

        private readonly byte[] _correlationData01;
        private readonly byte[] _correlationData02;
        private readonly byte[] _correlationData03;

        private readonly ReadOnlySequence<byte> _requestPayload01;
        private readonly ReadOnlySequence<byte> _requestPayload02;
        private readonly ReadOnlySequence<byte> _requestPayload03;

        private readonly ReadOnlySequence<byte> _responsePayload01;
        private readonly ReadOnlySequence<byte> _responsePayload02;
        private readonly ReadOnlySequence<byte> _responsePayload03;

        private readonly DateTime _futureExpirationTime;
        private readonly DateTime _pastExpirationTime;
        private readonly DateTime _futureStaleness;
        private readonly DateTime _pastStaleness;

        private readonly TimeSpan _cmdExecutionDuration;

        private readonly MqttApplicationMessage _responseMessage01;
        private readonly MqttApplicationMessage _responseMessage02;
        private readonly MqttApplicationMessage _responseMessage03;

        public CommandResponseCacheUnitTests()
        {
            _correlationData01 = Encoding.UTF8.GetBytes("correlation01");
            _correlationData02 = Encoding.UTF8.GetBytes("correlation02");
            _correlationData03 = Encoding.UTF8.GetBytes("correlation03");

            _requestPayload01 = new(Encoding.UTF8.GetBytes("request payload 01"));
            _requestPayload02 = new(Encoding.UTF8.GetBytes("request payload 02"));
            _requestPayload03 = new(Encoding.UTF8.GetBytes("request payload 03"));

            _responsePayload01 = new(Encoding.UTF8.GetBytes("response payload 01"));
            _responsePayload02 = new(Encoding.UTF8.GetBytes("response payload 02"));
            _responsePayload03 = new(Encoding.UTF8.GetBytes("response payload 03"));

            _futureExpirationTime = DateTime.UtcNow + TimeSpan.FromHours(1);
            _pastExpirationTime = DateTime.UtcNow - TimeSpan.FromHours(1);
            _futureStaleness = DateTime.UtcNow + TimeSpan.FromHours(1);
            _pastStaleness = DateTime.UtcNow - TimeSpan.FromHours(1);

            _cmdExecutionDuration = TimeSpan.FromSeconds(10);

            _responseMessage01 = new MqttApplicationMessage(MqttTopic1) { CorrelationData = _correlationData01, Payload = _responsePayload01 };
            _responseMessage02 = new MqttApplicationMessage(MqttTopic1) { CorrelationData = _correlationData02, Payload = _responsePayload02 };
            _responseMessage03 = new MqttApplicationMessage(MqttTopic1) { CorrelationData = _correlationData03, Payload = _responsePayload03 };
        }

        [Fact]
        public async Task NotStartedCacheThrowsExceptionOnStore()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            bool isIdempotent = true;
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration));
            Assert.Equal(AkriMqttErrorKind.StateInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
        }

        [Fact]
        public async Task NotStartedCacheDoesNotThrowOnRetrieve()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
        }

        [Fact]
        public async Task StoppedCacheThrowsExceptionOnStore()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            await commandResponseCache.StartAsync();
            await commandResponseCache.StopAsync();

            bool isIdempotent = true;
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration));
            Assert.Equal(AkriMqttErrorKind.StateInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
        }

        [Fact]
        public async Task StoppedCacheDoesNotThrowOnRetrieve()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            await commandResponseCache.StartAsync();
            await commandResponseCache.StopAsync();

            await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
        }

        [Fact]
        public async Task DedupCachesByValueNotByReference()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1.ToString(), InvokerId1.ToString(), MqttTopic1, _correlationData01.ToArray(), _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1.ToString(), InvokerId1.ToString(), MqttTopic1, _correlationData01.ToArray(), _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage message = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(message.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupCachesNullPayload()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupBySameTopicSucceedsWithReuseAcrossTopicsOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: true));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: true);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupBySameTopicSucceedsWithoutReuseAcrossTopicsOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupByDifferentTopicFailsWithReuseAcrossTopicsOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: true));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic2, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: true);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupByDifferentTopicFailsWithoutReuseAcrossTopicsOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic2, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseByDifferentInvokerFailsWithoutReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, ReadOnlySequence<byte>.Empty, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, MqttTopic1, _correlationData02, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task UnexpiredUncacheableMessageIsRetrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: false, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task UnexpiredUncacheableMessageIsNotRetrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiredOverlyStaleMessageIsNotStored()
        {
            var commandResponseCache = new TestCommandResponseCache();

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _pastExpirationTime,_cmdExecutionDuration);
            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task UnexpiredOverlyStaleMessageIsRetrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task UnexpiredOverlyStaleMessageIsNotRetrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsFutureForDedupWhenCacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.NotNull(cachedTask2);
            Assert.True(cachedTask2.Status == TaskStatus.WaitingForActivation);

            bool isIdempotent = false;
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _pastExpirationTime,_cmdExecutionDuration);

            Assert.True(cachedTask2.Status == TaskStatus.RanToCompletion);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsFutureForDedupWhenUncacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.NotNull(cachedTask2);
            Assert.True(cachedTask2.Status == TaskStatus.WaitingForActivation);

            bool isIdempotent = false;
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _pastExpirationTime,_cmdExecutionDuration);

            Assert.True(cachedTask2.Status == TaskStatus.RanToCompletion);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsNullForReuseWhenUncacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.Null(cachedTask2);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RetrieveForDedupRequiresMatchingTopic()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic2, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RetrieveForDedupDoesNotRequireMatchingCommandName()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName2, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheNotTrimmedOnStoreWhenSufficientEntriesAndSpace()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 3;
            commandResponseCache.MaxAggregatePayloadBytes = 160;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(Enumerable.SequenceEqual(cachedMessage3.Payload.ToArray(), _responsePayload03.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheNotTrimmedOnStoreWhenNoEntriesEligibleForEviction()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 2;
            commandResponseCache.MaxAggregatePayloadBytes = 130;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(Enumerable.SequenceEqual(cachedMessage3.Payload.ToArray(), _responsePayload03.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnLowValueStoreWhenTooManyEntries()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 2;
            commandResponseCache.MaxAggregatePayloadBytes = 160;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.Null(cachedTask3);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnHighValueStoreWhenTooManyEntries()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 2;
            commandResponseCache.MaxAggregatePayloadBytes = 160;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.Null(cachedTask2);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(Enumerable.SequenceEqual(cachedMessage3.Payload.ToArray(), _responsePayload03.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnLowValueStoreWhenTooMuchSpace()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 3;
            commandResponseCache.MaxAggregatePayloadBytes = 130;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.Null(cachedTask3);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnHighValueStoreWhenTooMuchSpace()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 3;
            commandResponseCache.MaxAggregatePayloadBytes = 130;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.Null(cachedTask2);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(Enumerable.SequenceEqual(cachedMessage3.Payload.ToArray(), _responsePayload03.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnLowValueExpiryWhenTooManyEntries()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 2;
            commandResponseCache.MaxAggregatePayloadBytes = 160;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + _temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, soonExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.Null(cachedTask3);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnHighValueExpiryWhenTooManyEntries()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 2;
            commandResponseCache.MaxAggregatePayloadBytes = 160;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + _temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, soonExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.Null(cachedTask3);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnLowValueExpiryWhenTooMuchSpace()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 3;
            commandResponseCache.MaxAggregatePayloadBytes = 130;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + _temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, soonExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.Null(cachedTask3);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheTrimmedOnHighValueExpiryWhenTooMuchSpace()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 3;
            commandResponseCache.MaxAggregatePayloadBytes = 130;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, _responseMessage02, isIdempotent, _futureExpirationTime, _cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + _temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, _responseMessage03, isIdempotent, soonExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData03, _requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(Enumerable.SequenceEqual(cachedMessage1.Payload.ToArray(), _responsePayload01.ToArray()));

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(Enumerable.SequenceEqual(cachedMessage2.Payload.ToArray(), _responsePayload02.ToArray()));

            Assert.Null(cachedTask3);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiringOverlyStaleMessageBecomesUnretrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExpirationTime = DateTime.UtcNow + _temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, soonExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask != null && --loopLimit > 0);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RefreshedExpiredMessageBecomesUnretrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExcessiveStaleness = DateTime.UtcNow + _temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _pastExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask != null && --loopLimit > 0);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RefreshedExpiredMessageBecomesUnretrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExcessiveStaleness = DateTime.UtcNow + _temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _pastExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask != null && --loopLimit > 0);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RefreshedUnexpiredMessageRemainsRetrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExcessiveStaleness = DateTime.UtcNow + _temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime,_cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await Task.Delay(_temporalTestQuiescenceDelay);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            cachedMessage = await cachedTask;
            Assert.True(Enumerable.SequenceEqual(cachedMessage.Payload.ToArray(), _responsePayload01.ToArray()));

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RefreshedUnexpiredMessageBecomesUnretrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExcessiveStaleness = DateTime.UtcNow + _temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData01, _requestPayload01, _responseMessage01, isIdempotent, _futureExpirationTime,_cmdExecutionDuration);

            int loopLimit = _temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(_temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, MqttTopic1, _correlationData02, _requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask != null && --loopLimit > 0);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }
    }
}
