// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib.UnitTests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.Opc2WotLib;
    using Xunit;

    public class OptionalAffordanceTests
    {
        private const string ModelUri = "http://opcfoundation.org/UA/OptionalTest/";

        // Self-contained OPC UA nodeset (base-type chain terminates locally, so the core
        // Opc.Ua nodeset is not required). "WidgetType" declares a mix of mandatory and
        // optional members, distinguished only by their HasModellingRule reference:
        //   - Mandatory (i=78): SerialNumber (property), Start (method)
        //   - Optional  (i=80): Note (property), Reset (method)
        // "GadgetType" declares only mandatory members, so it must emit no tm:optional.
        private const string Nodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/OptionalTest/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/OptionalTest/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/" Version="1.05.02" PublicationDate="2022-10-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasModellingRule">i=37</Alias>
                <Alias Alias="HasProperty">i=46</Alias>
                <Alias Alias="HasComponent">i=47</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:WidgetType">
                <References>
                  <Reference ReferenceType="HasProperty">ns=1;i=10</Reference>
                  <Reference ReferenceType="HasProperty">ns=1;i=11</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=20</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=21</Reference>
                </References>
              </UAObjectType>
              <UAObjectType NodeId="ns=1;i=2" BrowseName="1:GadgetType">
                <References>
                  <Reference ReferenceType="HasProperty">ns=1;i=30</Reference>
                </References>
              </UAObjectType>
              <UAVariable NodeId="ns=1;i=10" BrowseName="1:SerialNumber" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=11" BrowseName="1:Note" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=80</Reference>
                </References>
              </UAVariable>
              <UAMethod NodeId="ns=1;i=20" BrowseName="1:Start" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                </References>
              </UAMethod>
              <UAMethod NodeId="ns=1;i=21" BrowseName="1:Reset" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=80</Reference>
                </References>
              </UAMethod>
              <UAVariable NodeId="ns=1;i=30" BrowseName="1:Model" ParentNodeId="ns=1;i=2">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                </References>
              </UAVariable>
            </UANodeSet>
            """;

        [Fact]
        public void OptionalMembers_AreListedInTmOptional()
        {
            JsonElement widget = GetThingByTitleSuffix("WidgetType");

            Assert.True(widget.TryGetProperty("tm:optional", out JsonElement optional));
            string[] entries = optional.EnumerateArray().Select(e => e.GetString()!).ToArray();

            Assert.Contains("/properties/Note", entries);
            Assert.Contains("/actions/Reset", entries);
        }

        [Fact]
        public void MandatoryMembers_AreNotListedInTmOptional()
        {
            JsonElement widget = GetThingByTitleSuffix("WidgetType");

            Assert.True(widget.TryGetProperty("tm:optional", out JsonElement optional));
            string[] entries = optional.EnumerateArray().Select(e => e.GetString()!).ToArray();

            Assert.DoesNotContain("/properties/SerialNumber", entries);
            Assert.DoesNotContain("/actions/Start", entries);
        }

        [Fact]
        public void TypeWithOnlyMandatoryMembers_EmitsNoTmOptional()
        {
            JsonElement gadget = GetThingByTitleSuffix("GadgetType");

            // The affordance itself must be present (guards against a false pass from an empty Thing).
            Assert.True(gadget.GetProperty("properties").TryGetProperty("Model", out _));
            Assert.False(gadget.TryGetProperty("tm:optional", out _));
        }

        private static JsonElement GetThingByTitleSuffix(string titleSuffix)
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(Nodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(ModelUri),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement thing = doc.RootElement.EnumerateArray()
                .Single(t => t.GetProperty("title").GetString()!.EndsWith(titleSuffix, StringComparison.Ordinal));

            // Clone so the element remains usable after the JsonDocument is disposed.
            return thing.Clone();
        }
    }
}
