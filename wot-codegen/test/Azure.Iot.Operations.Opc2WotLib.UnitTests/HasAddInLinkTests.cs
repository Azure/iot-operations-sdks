// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib.UnitTests
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.Opc2Wot;
    using Azure.Iot.Operations.Opc2WotLib;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using Xunit;

    public class HasAddInLinkTests
    {
        private const string ModelUri = "http://opcfoundation.org/UA/AddInTest/";
        private const string ReferencingModelUri = "http://opcfoundation.org/UA/ReferenceOrderTest/";

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

        private const string ReferencingNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/ReferenceOrderTest/</Uri>
                <Uri>http://opcfoundation.org/UA/ReferencedModel/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/ReferenceOrderTest/" Version="1.0.0">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/ReferencedModel/" Version="1.0.0" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasComponent">i=47</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:ContainerType">
                <References>
                  <Reference ReferenceType="HasComponent">ns=2;i=100</Reference>
                </References>
              </UAObjectType>
            </UANodeSet>
            """;

        private const string ReferencedNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/ReferencedModel/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/ReferencedModel/" Version="1.0.0" />
              </Models>
              <Aliases>
                <Alias Alias="HasTypeDefinition">i=40</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=2" BrowseName="1:ModuleType" />
              <UAObject NodeId="ns=1;i=100" BrowseName="1:Module">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=2</Reference>
                </References>
              </UAObject>
            </UANodeSet>
            """;

        private const string DuplicateTypeNameNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/DuplicateTypeName/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/DuplicateTypeName/" Version="1.0.0" />
              </Models>
              <Aliases>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="ModuleType" />
              <UAObjectType NodeId="ns=1;i=2" BrowseName="ModuleType" />
              <UAObjectType NodeId="ns=1;i=3" BrowseName="ModuleType_2" />
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
            Assert.Equal(
                WotUtil.GetThingModelId(WotUtil.GetTypeRef(ModelUri, "IdentificationType")),
                href);
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

        [Fact]
        public void ReferencedModel_CanBeLoadedAfterReferencingModel()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(ReferencingNodeset);
            graph.AddNodeset(ReferencedNodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(ReferencingModelUri),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement container = doc.RootElement.EnumerateArray()
                .Single(t => t.GetProperty("title").GetString()!.EndsWith("ContainerType", System.StringComparison.Ordinal));
            JsonElement link = Assert.Single(container.GetProperty("links").EnumerateArray().Select(l => l.Clone()));

            string href = link.GetProperty("href").GetString()!;
            Assert.True(Uri.TryCreate(href, UriKind.Absolute, out _));
            Assert.Equal(
                WotUtil.GetThingModelId(WotUtil.GetTypeRef("http://opcfoundation.org/UA/ReferencedModel/", "ModuleType")),
                href);
        }

        [Fact]
        public void DuplicateTypeNames_HaveMatchingUniqueIdsAndTypeRefs()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(DuplicateTypeNameNodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo("http://opcfoundation.org/UA/DuplicateTypeName/"),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement[] models = doc.RootElement.EnumerateArray()
                .OrderBy(model => model.GetProperty("title").GetString(), StringComparer.Ordinal)
                .Select(model => model.Clone())
                .ToArray();

            Assert.Equal(3, models.Length);
            Assert.Equal(3, models.Select(model => model.GetProperty("dov:typeRef").GetString()).Distinct().Count());
            Assert.Equal(
                models.Select(model => $"urn:{model.GetProperty("dov:typeRef").GetString()}"),
                models.Select(model => model.GetProperty("id").GetString()));
        }

        [Fact]
        public void CommandHandler_WritesStandaloneThingModelsWithIdBasedReferences()
        {
            string sandboxPath = Path.Combine(Path.GetTempPath(), $"Opc2WotThingModelOutputTests_{Guid.NewGuid():N}");
            DirectoryInfo sandbox = Directory.CreateDirectory(sandboxPath);

            try
            {
                string referencingPath = Path.Combine(sandbox.FullName, "Referencing.NodeSet2.xml");
                string referencedPath = Path.Combine(sandbox.FullName, "Referenced.NodeSet2.xml");
                File.WriteAllText(referencingPath, ReferencingNodeset);
                File.WriteAllText(referencedPath, ReferencedNodeset);
                DirectoryInfo outputDir = new DirectoryInfo(Path.Combine(sandbox.FullName, "out"));

                OptionContainer options = new OptionContainer
                {
                    NodeSetsSpec = new[] { referencingPath, referencedPath },
                    OutputDir = outputDir,
                    Integrate = false,
                    InheritVars = false,
                    IncludeTDs = false,
                };

                var errorLog = CommandHandler.ConvertSpecs(options, (_, _) => { });

                Assert.False(errorLog.HasErrors);
                Assert.Equal(
                    new[] { "ReferenceOrderTest_ContainerType.TM.json", "ReferencedModel_ModuleType.TM.json" },
                    outputDir.GetFiles("*.TM.json").Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));

                using JsonDocument containerDocument = JsonDocument.Parse(
                    File.ReadAllText(Path.Combine(outputDir.FullName, "ReferenceOrderTest_ContainerType.TM.json")));
                using JsonDocument moduleDocument = JsonDocument.Parse(
                    File.ReadAllText(Path.Combine(outputDir.FullName, "ReferencedModel_ModuleType.TM.json")));

                Assert.Equal(JsonValueKind.Object, containerDocument.RootElement.ValueKind);
                Assert.Equal(JsonValueKind.Object, moduleDocument.RootElement.ValueKind);

                string moduleId = moduleDocument.RootElement.GetProperty("id").GetString()!;
                string linkHref = Assert.Single(
                    containerDocument.RootElement.GetProperty("links").EnumerateArray().Select(link => link.Clone()))
                    .GetProperty("href")
                    .GetString()!;

                Assert.True(Uri.TryCreate(moduleId, UriKind.Absolute, out _));
                Assert.Equal(
                    WotUtil.GetThingModelId(WotUtil.GetTypeRef("http://opcfoundation.org/UA/ReferencedModel/", "ModuleType")),
                    moduleId);
                Assert.Equal("ReferencedModel_ModuleType", moduleDocument.RootElement.GetProperty("title").GetString());
                Assert.Equal(
                    $"urn:{moduleDocument.RootElement.GetProperty("dov:typeRef").GetString()}",
                    moduleId);
                Assert.Equal(moduleId, linkHref);
            }
            finally
            {
                sandbox.Delete(recursive: true);
            }
        }

        [Fact]
        public void StandaloneThingModel_PreservesLiteralJsonCharacters()
        {
            WotThingDocument document = WotThingDocument.Create(
                "Test.TM.json",
                """{"links":[{"dov:refName":"<Node>","type":"application/tm+json"}]}""");

            Assert.Contains("\"dov:refName\": \"<Node>\"", document.Text);
            Assert.Contains("\"type\": \"application/tm+json\"", document.Text);
            Assert.DoesNotContain(@"\u003C", document.Text);
            Assert.DoesNotContain(@"\u002B", document.Text);
        }

        [Fact]
        public void StandaloneThingModel_OmitsProtocolForms()
        {
            const string thingText = """
                {
                  "forms": [{ "op": "readallproperties" }],
                  "actions": { "Run": { "forms": [{ "op": "invokeaction" }] } },
                  "properties": { "Status": { "forms": [{ "op": "readproperty" }] } },
                  "events": { "Changed": { "forms": [{ "op": "subscribeevent" }] } }
                }
                """;

            WotThingDocument thingModel = WotThingDocument.Create("Test.TM.json", thingText);
            WotThingDocument thingDescription = WotThingDocument.Create("Test.TD.json", thingText);

            Assert.DoesNotContain("\"forms\"", thingModel.Text);
            Assert.Contains("\"forms\"", thingDescription.Text);
        }

        [Fact]
        public void ThingValidator_AcceptsAffordancesWithoutFormsInThingModel()
        {
            const string thingText = """
                {
                  "@context": [
                    "https://www.w3.org/2022/wot/td/v1.1",
                    { "dov": "http://azure.com/DigitalOperations/vocab#" }
                  ],
                  "@type": "tm:ThingModel",
                  "title": "FormFreeModel",
                  "properties": {
                    "Status": {
                      "type": "string",
                      "readOnly": true
                    }
                  }
                }
                """;
            byte[] thingBytes = Encoding.UTF8.GetBytes(thingText);
            ErrorLog errorLog = new(string.Empty);
            ErrorReporter errorReporter = new(errorLog, "FormFreeModel.TM.json", thingBytes);
            TDThing thing = Assert.Single(TDParser.Parse(thingBytes));
            Dictionary<string, TDThing> hrefToThingMap = new()
            {
                ["#title=FormFreeModel"] = thing,
            };
            ThingValidator validator = new(errorReporter, requireThingModelForms: false);

            bool isValid = validator.TryValidateThing(
                new IntegralResolvingThing(thing, errorReporter, hrefToThingMap),
                new HashSet<SerializationFormat>(),
                validateReferences: false);

            Assert.True(isValid, string.Join(System.Environment.NewLine, errorLog.Errors.Select(error => error.Message)));
            Assert.False(errorLog.HasErrors);
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
