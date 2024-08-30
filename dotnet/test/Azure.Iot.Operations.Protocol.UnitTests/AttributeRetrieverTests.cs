namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class AttributeRetrieverTests
    {
        [ModelId("id-top")]
        [CommandTopic("cmd-top")]
        public class TopLevelClass
        {
            [ModelId("id-mid")]
            [TelemetryTopic("telem-mid")]
            public class MidLevelClass
            {
                [ModelId("id-leaf")]
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
            Assert.True(AttributeRetriever.HasAttribute<ModelIdAttribute>(leaf));
            ModelIdAttribute? attr = AttributeRetriever.GetAttribute<ModelIdAttribute>(leaf);
            Assert.NotNull(attr);
            Assert.Equal("id-leaf", attr.Id);
        }

        [Fact]
        public void SelfTakesPrecedenceOverParentAndChild()
        {
            TopLevelClass.MidLevelClass mid = new();
            Assert.True(AttributeRetriever.HasAttribute<ModelIdAttribute>(mid));
            ModelIdAttribute? attr = AttributeRetriever.GetAttribute<ModelIdAttribute>(mid);
            Assert.NotNull(attr);
            Assert.Equal("id-mid", attr.Id);
        }

        [Fact]
        public void NullIfNowhereInHierarchy()
        {
            TopLevelClass.MidLevelClass mid = new();
            Assert.False(AttributeRetriever.HasAttribute<CommandBehaviorAttribute>(mid));
            CommandBehaviorAttribute? attr = AttributeRetriever.GetAttribute<CommandBehaviorAttribute>(mid);
            Assert.Null(attr);
        }

        [Fact]
        public void NullIfNotSelfOrAboveInHierarchy()
        {
            TopLevelClass.MidLevelClass mid = new();
            Assert.False(AttributeRetriever.HasAttribute<ServiceGroupIdAttribute>(mid));
            ServiceGroupIdAttribute? attr = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(mid);
            Assert.Null(attr);
        }
    }
}
