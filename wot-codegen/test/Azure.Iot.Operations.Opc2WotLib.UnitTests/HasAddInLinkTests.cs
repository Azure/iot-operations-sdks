// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib.UnitTests
{
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.Opc2WotLib;
    using Xunit;

    public class HasAddInLinkTests
    {
        private const string ModelUri = "http://opcfoundation.org/UA/AddInTest/";

        // Minimal, self-contained OPC UA nodeset. The base-type chain terminates locally
        // (no HasSubtype to a core ns=0 type), so the core Opc.Ua nodeset is not required.
        // MachineToolType-style source ("MachineType") composes two functional modules:
        //   - "Identification" via HasAddIn (ns=0; i=17604)
        //   - "Diagnostics"    via HasComponent (ns=0; i=47) for contrast
        private const string Nodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/AddInTest/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/AddInTest/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/" Version="1.05.02" PublicationDate="2022-10-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasModellingRule">i=37</Alias>
                <Alias Alias="HasTypeDefinition">i=40</Alias>
                <Alias Alias="HasSubtype">i=45</Alias>
                <Alias Alias="HasProperty">i=46</Alias>
                <Alias Alias="HasComponent">i=47</Alias>
                <Alias Alias="HasAddIn">i=17604</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:MachineType">
                <References>
                  <Reference ReferenceType="HasAddIn">ns=1;i=100</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=101</Reference>
                </References>
              </UAObjectType>
              <UAObjectType NodeId="ns=1;i=2" BrowseName="1:IdentificationType" />
              <UAObjectType NodeId="ns=1;i=3" BrowseName="1:DiagnosticsType" />
              <UAObject NodeId="ns=1;i=100" BrowseName="1:Identification">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=2</Reference>
                </References>
              </UAObject>
              <UAObject NodeId="ns=1;i=101" BrowseName="1:Diagnostics">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=3</Reference>
                </References>
              </UAObject>
            </UANodeSet>
            """;

        [Fact]
        public void HasAddInReference_IsPreservedAsWotLink()
        {
            JsonElement machineType = GetThingByTitleSuffix("MachineType");
            JsonElement links = machineType.GetProperty("links");

            JsonElement? addInLink = FindLinkByRefName(links, "Identification");
            Assert.True(addInLink.HasValue, "HasAddIn add-in 'Identification' must be emitted as a WoT link.");

            // The add-in link must carry a relationship and point at the add-in's type definition.
            string rel = addInLink.Value.GetProperty("rel").GetString()!;
            Assert.StartsWith("dov:", rel);
            string href = addInLink.Value.GetProperty("href").GetString()!;
            Assert.Contains("IdentificationType", href);
        }

        [Fact]
        public void HasComponentReference_IsStillPreservedAsWotLink()
        {
            // Guards against regressing the existing HasComponent handling while adding HasAddIn.
            JsonElement machineType = GetThingByTitleSuffix("MachineType");
            JsonElement links = machineType.GetProperty("links");

            JsonElement? componentLink = FindLinkByRefName(links, "Diagnostics");
            Assert.True(componentLink.HasValue, "HasComponent child 'Diagnostics' must be emitted as a WoT link.");
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
                .Single(t => t.GetProperty("title").GetString()!.EndsWith(titleSuffix, System.StringComparison.Ordinal));

            // Clone so the element remains usable after the JsonDocument is disposed.
            return thing.Clone();
        }

        private static JsonElement? FindLinkByRefName(JsonElement links, string refName)
        {
            foreach (JsonElement link in links.EnumerateArray())
            {
                if (link.TryGetProperty("dov:refName", out JsonElement refNameElt) &&
                    refNameElt.GetString() == refName)
                {
                    return link;
                }
            }

            return null;
        }
    }
}
