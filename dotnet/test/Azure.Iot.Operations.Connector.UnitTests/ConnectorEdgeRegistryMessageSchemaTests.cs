// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class ConnectorEdgeRegistryMessageSchemaTests
    {
        // Pins the format every SDK must produce, so that connectors in different languages derive the
        // same Schema identifier for the same data operation.
        [Fact]
        public void DefaultDatasetSchemaIdIdentifiesTheDataOperation()
        {
            Assert.Equal(
                "my-device:my-endpoint:my-asset:dataset:my-dataset",
                ConnectorEdgeRegistryMessageSchema.DefaultDatasetSchemaId("my-device", "my-endpoint", "my-asset", "my-dataset"));
        }

        [Fact]
        public void DefaultEventSchemaIdIdentifiesTheDataOperation()
        {
            Assert.Equal(
                "my-device:my-endpoint:my-asset:event:my-event-group:my-event",
                ConnectorEdgeRegistryMessageSchema.DefaultEventSchemaId("my-device", "my-endpoint", "my-asset", "my-event-group", "my-event"));
        }
    }
}
