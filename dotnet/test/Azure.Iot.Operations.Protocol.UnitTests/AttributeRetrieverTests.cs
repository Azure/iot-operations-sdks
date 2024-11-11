namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class AttributeRetrieverTests
    {
        [ServiceGroupId("group-top")]
        [CommandTopic("cmd-top")]
        public class TopLevelClass
        {
            [ServiceGroupId("group-mid")]
            [TelemetryTopic("telem-mid")]
            public class MidLevelClass
            {
                [ServiceGroupId("group-leaf")]
                public class LeafClass
                {
                }
            }
        }

        [Fact]
        public void RetrieveFromSelf()
        {
            TopLevelClass.MidLevelClass.LeafClass leaf = new();
            Assert.True(AttributeRetriever.HasAttribute<ServiceGroupIdAttribute>(leaf));
            ServiceGroupIdAttribute? attr = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(leaf);
            Assert.NotNull(attr);
            Assert.Equal("group-leaf", attr.Id);
        }

        [Fact]
        public void RetrieveFromParent()
        {
            TopLevelClass.MidLevelClass.LeafClass leaf = new();
            Assert.True(AttributeRetriever.HasAttribute<TelemetryTopicAttribute>(leaf));
            TelemetryTopicAttribute? attr = AttributeRetriever.GetAttribute<TelemetryTopicAttribute>(leaf);
            Assert.NotNull(attr);
            Assert.Equal("telem-mid", attr.Topic);
        }

        [Fact]
        public void RetrieveFromGrandparent()
        {
            TopLevelClass.MidLevelClass.LeafClass leaf = new();
            Assert.True(AttributeRetriever.HasAttribute<CommandTopicAttribute>(leaf));
            CommandTopicAttribute? attr = AttributeRetriever.GetAttribute<CommandTopicAttribute>(leaf);
            Assert.NotNull(attr);
            Assert.Equal("cmd-top", attr.RequestTopic);
        }

        [Fact]
        public void SelfTakesPrecedenceOverParentAndGrandparent()
        {
            TopLevelClass.MidLevelClass.LeafClass leaf = new();
            Assert.True(AttributeRetriever.HasAttribute<ServiceGroupIdAttribute>(leaf));
            ServiceGroupIdAttribute? attr = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(leaf);
            Assert.NotNull(attr);
            Assert.Equal("group-leaf", attr.Id);
        }

        [Fact]
        public void SelfTakesPrecedenceOverParentAndChild()
        {
            TopLevelClass.MidLevelClass mid = new();
            Assert.True(AttributeRetriever.HasAttribute<ServiceGroupIdAttribute>(mid));
            ServiceGroupIdAttribute? attr = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(mid);
            Assert.NotNull(attr);
            Assert.Equal("group-mid", attr.Id);
        }

        [Fact]
        public void NullIfNowhereInHierarchy()
        {
            TopLevelClass.MidLevelClass mid = new();
            Assert.False(AttributeRetriever.HasAttribute<CommandBehaviorAttribute>(mid));
            CommandBehaviorAttribute? attr = AttributeRetriever.GetAttribute<CommandBehaviorAttribute>(mid);
            Assert.Null(attr);
        }
    }
}
