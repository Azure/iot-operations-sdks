namespace Azure.Iot.Operations.Protocol.UnitTests
{
    using System.Text;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Protocol.RPC;

    public class CommandResponseCacheUnitTests
    {
        private class TestCommandResponseCache : CommandResponseCache
        {
            public double? CachingBenefit { get; set; }

            public override double CostWeightedBenefit(byte[]? requestPayload, MqttApplicationMessage responseMessage, TimeSpan executionDuration)
            {
                return this.CachingBenefit != null ? (double)this.CachingBenefit : base.CostWeightedBenefit(requestPayload, responseMessage, executionDuration);
            }
        }

        private const string CommonMqttTopic = "some/topic";

        private const string CommandName1 = "Command1";
        private const string CommandName2 = "Command2";

        private const string InvokerId1 = "Invoker1";
        private const string InvokerId2 = "Invoker2";

        private readonly TimeSpan temporalTestSoonDuration = TimeSpan.FromSeconds(0.25);
        private readonly TimeSpan temporalTestQuiescenceDelay = TimeSpan.FromSeconds(1);
        private readonly TimeSpan temporalTestLoopDelay = TimeSpan.FromSeconds(0.1);
        private readonly int temporalTestLoopLimit = 10;

        private readonly byte[] correlationData01;
        private readonly byte[] correlationData02;
        private readonly byte[] correlationData03;

        private readonly byte[] requestPayload01;
        private readonly byte[] requestPayload02;
        private readonly byte[] requestPayload03;

        private readonly byte[] responsePayload01;
        private readonly byte[] responsePayload02;
        private readonly byte[] responsePayload03;

        private readonly DateTime futureExpirationTime;
        private readonly DateTime pastExpirationTime;
        private readonly DateTime futureStaleness;
        private readonly DateTime pastStaleness;

        private readonly TimeSpan cmdExecutionDuration;

        private readonly MqttApplicationMessage responseMessage01;
        private readonly MqttApplicationMessage responseMessage02;
        private readonly MqttApplicationMessage responseMessage03;

        public CommandResponseCacheUnitTests()
        {
            this.correlationData01 = Encoding.UTF8.GetBytes("correlation01");
            this.correlationData02 = Encoding.UTF8.GetBytes("correlation02");
            this.correlationData03 = Encoding.UTF8.GetBytes("correlation03");

            this.requestPayload01 = Encoding.UTF8.GetBytes("request payload 01");
            this.requestPayload02 = Encoding.UTF8.GetBytes("request payload 02");
            this.requestPayload03 = Encoding.UTF8.GetBytes("request payload 03");

            this.responsePayload01 = Encoding.UTF8.GetBytes("response payload 01");
            this.responsePayload02 = Encoding.UTF8.GetBytes("response payload 02");
            this.responsePayload03 = Encoding.UTF8.GetBytes("response payload 03");

            this.futureExpirationTime = DateTime.UtcNow + TimeSpan.FromHours(1);
            this.pastExpirationTime = DateTime.UtcNow - TimeSpan.FromHours(1);
            this.futureStaleness = DateTime.UtcNow + TimeSpan.FromHours(1);
            this.pastStaleness = DateTime.UtcNow - TimeSpan.FromHours(1);

            this.cmdExecutionDuration = TimeSpan.FromSeconds(10);

            this.responseMessage01 = new MqttApplicationMessage(CommonMqttTopic) { CorrelationData = this.correlationData01, PayloadSegment = this.responsePayload01 };
            this.responseMessage02 = new MqttApplicationMessage(CommonMqttTopic) { CorrelationData = this.correlationData02, PayloadSegment = this.responsePayload02 };
            this.responseMessage03 = new MqttApplicationMessage(CommonMqttTopic) { CorrelationData = this.correlationData03, PayloadSegment = this.responsePayload03 };
        }

        [Fact]
        public async Task NotStartedCacheThrowsExceptionOnStore()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            bool isIdempotent = true;
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration));
            Assert.Equal(AkriMqttErrorKind.StateInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
        }

        [Fact]
        public async Task NotStartedCacheDoesNotThrowOnRetrieve()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
        }

        [Fact]
        public async Task StoppedCacheThrowsExceptionOnStore()
        {
            CommandResponseCache commandResponseCache = new CommandResponseCache();

            await commandResponseCache.StartAsync();
            await commandResponseCache.StopAsync();

            bool isIdempotent = true;
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration));
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

            await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
        }

        [Fact]
        public async Task DedupCachesByValueNotByReference()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1.ToString(), InvokerId1.ToString(), this.correlationData01.ToArray(), this.requestPayload01.ToArray(), isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1.ToString(), InvokerId1.ToString(), this.correlationData01.ToArray(), this.requestPayload02.ToArray(), isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage message = await cachedTask;
            Assert.True(message.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseCachesByValueNotByReference()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1.ToString(), InvokerId1.ToString(), this.correlationData01.ToArray(), this.requestPayload01.ToArray(), isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1.ToString(), InvokerId1.ToString(), this.correlationData02.ToArray(), this.requestPayload01.ToArray(), isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupCachesNullPayload()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, null!, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, null!, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseCachesNullPayload()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, null!, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, null!, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupBySameInvokerSucceedsWithReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: true));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: true);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupBySameInvokerSucceedsWithoutReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupByDifferentInvokerFailsWithReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: true));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: true);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task DedupByDifferentInvokerFailsWithoutReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseBySameInvokerSucceedsWithReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: true));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: true);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseBySameInvokerSucceedsWithoutReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseByDifferentInvokerSucceedsWithReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: true));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: true);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ReuseByDifferentInvokerFailsWithoutReuseAcrossInvokersOption()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, null!, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: false, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task UnexpiredUncacheableMessageIsNotRetrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiredOverlyStaleMessageIsNotStored()
        {
            var commandResponseCache = new TestCommandResponseCache();

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, this.pastStaleness, this.cmdExecutionDuration);
            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.pastStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task UnexpiredOverlyStaleMessageIsNotRetrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.pastStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiredSlightlyStaleMessageIsRetrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiredSlightlyStaleMessageIsRetrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsFutureForDedupWhenCacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.NotNull(cachedTask2);
            Assert.True(cachedTask2.Status == TaskStatus.WaitingForActivation);

            bool isIdempotent = false;
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, this.pastStaleness, this.cmdExecutionDuration);

            Assert.True(cachedTask2.Status == TaskStatus.RanToCompletion);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsFutureForReuseWhenCacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.NotNull(cachedTask2);
            Assert.True(cachedTask2.Status == TaskStatus.WaitingForActivation);

            bool isIdempotent = false;
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, this.pastStaleness, this.cmdExecutionDuration);

            Assert.True(cachedTask2.Status == TaskStatus.RanToCompletion);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsFutureForDedupWhenUncacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.NotNull(cachedTask2);
            Assert.True(cachedTask2.Status == TaskStatus.WaitingForActivation);

            bool isIdempotent = false;
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, this.pastStaleness, this.cmdExecutionDuration);

            Assert.True(cachedTask2.Status == TaskStatus.RanToCompletion);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task SecondRetrieveReturnsNullForReuseWhenUncacheable()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: false, canReuseAcrossInvokers: false);

            Assert.Null(cachedTask1);
            Assert.Null(cachedTask2);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RetrieveForDedupRequiresMatchingInvokerId()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RetrieveForDedupDoesNotRequireMatchingCommandName()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName2, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RetrieveForReuseRequiresMatchingInvokerId()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId2, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RetrieveForReuseRequiresMatchingCommandName()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName2, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.Null(cachedTask);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(cachedMessage3.PayloadSegment == responsePayload03);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(cachedMessage3.PayloadSegment == responsePayload03);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.Null(cachedTask2);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(cachedMessage3.PayloadSegment == responsePayload03);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.Null(cachedTask2);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(cachedMessage3.PayloadSegment == responsePayload03);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task CacheNotTrimmedOnExpiryWhenSufficientEntriesAndSpace()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.MaxEntryCount = 3;
            commandResponseCache.MaxAggregatePayloadBytes = 160;

            await commandResponseCache.StartAsync();

            bool isIdempotent = false;

            commandResponseCache.CachingBenefit = 0.5;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            await Task.Delay(this.temporalTestQuiescenceDelay);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

            Assert.NotNull(cachedTask3);
            MqttApplicationMessage cachedMessage3 = await cachedTask3;
            Assert.True(cachedMessage3.PayloadSegment == responsePayload03);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.3;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

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
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            commandResponseCache.CachingBenefit = 0.4;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, this.responseMessage02, isIdempotent, this.futureExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            commandResponseCache.CachingBenefit = 0.6;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, this.responseMessage03, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask3;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask3 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData03, this.requestPayload03, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask3 != null && --loopLimit > 0);

            Task<MqttApplicationMessage>? cachedTask1 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Task<MqttApplicationMessage>? cachedTask2 = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);

            Assert.NotNull(cachedTask1);
            MqttApplicationMessage cachedMessage1 = await cachedTask1;
            Assert.True(cachedMessage1.PayloadSegment == responsePayload01);

            Assert.NotNull(cachedTask2);
            MqttApplicationMessage cachedMessage2 = await cachedTask2;
            Assert.True(cachedMessage2.PayloadSegment == responsePayload02);

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
            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, soonExpirationTime, this.pastStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask != null && --loopLimit > 0);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiringSlightlyStaleMessageRemainsRetrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await Task.Delay(this.temporalTestQuiescenceDelay);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task ExpiringSlightlyStaleMessageRemainsRetrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExpirationTime = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, soonExpirationTime, this.futureStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await Task.Delay(this.temporalTestQuiescenceDelay);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RefreshedExpiredMessageBecomesUnretrievableForDedup()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExcessiveStaleness = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, soonExcessiveStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
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
            DateTime soonExcessiveStaleness = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.pastExpirationTime, soonExcessiveStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
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
            DateTime soonExcessiveStaleness = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, soonExcessiveStaleness, this.cmdExecutionDuration);

            Task<MqttApplicationMessage>? cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            MqttApplicationMessage cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await Task.Delay(this.temporalTestQuiescenceDelay);

            cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload02, isCacheable: true, canReuseAcrossInvokers: false);
            Assert.NotNull(cachedTask);
            cachedMessage = await cachedTask;
            Assert.True(cachedMessage.PayloadSegment == responsePayload01);

            await commandResponseCache.StopAsync();
        }

        [Fact]
        public async Task RefreshedUnexpiredMessageBecomesUnretrievableForReuse()
        {
            var commandResponseCache = new TestCommandResponseCache();
            commandResponseCache.CachingBenefit = 0.3;

            await commandResponseCache.StartAsync();

            bool isIdempotent = true;
            DateTime soonExcessiveStaleness = DateTime.UtcNow + this.temporalTestSoonDuration;
            Assert.Null(await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false));
            await commandResponseCache.StoreAsync(CommandName1, InvokerId1, this.correlationData01, this.requestPayload01, this.responseMessage01, isIdempotent, this.futureExpirationTime, soonExcessiveStaleness, this.cmdExecutionDuration);

            int loopLimit = this.temporalTestLoopLimit;
            Task<MqttApplicationMessage>? cachedTask;
            do
            {
                await Task.Delay(this.temporalTestLoopDelay);
                cachedTask = await commandResponseCache.RetrieveAsync(CommandName1, InvokerId1, this.correlationData02, this.requestPayload01, isCacheable: true, canReuseAcrossInvokers: false);
            } while (cachedTask != null && --loopLimit > 0);

            Assert.Null(cachedTask);

            await commandResponseCache.StopAsync();
        }
    }
}