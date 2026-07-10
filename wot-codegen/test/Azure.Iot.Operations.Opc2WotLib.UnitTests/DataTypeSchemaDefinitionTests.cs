// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.Opc2Wot;
    using Azure.Iot.Operations.Opc2WotLib;
    using Xunit;

    public class DataTypeSchemaDefinitionTests
    {
        private const string ModelUri = "http://opcfoundation.org/UA/DataTypeTest/";

        // Self-contained OPC UA nodeset exercising every non-built-in UADataType kind.
        // The DataType base-type chains reference only ns=0 built-ins and every field/base
        // resolves to a primitive, so the core Opc.Ua nodeset is not required.
        //
        //   - ReferencedEnum  (ns=1;i=50): enum used by a Variable on WidgetType itself.
        //   - InstanceOnlyEnum(ns=1;i=51): enum used ONLY by a Variable on a concrete UAObject
        //                                  instance (the "ToolLocked" scenario).
        //   - OrphanEnum      (ns=1;i=52): enum referenced by nothing.
        //   - WidgetStruct    (ns=1;i=53): structured (object) DataType with primitive fields.
        //   - TemperatureType (ns=1;i=54): simple-type alias (subtype of Double).
        //   - BaseStruct      (ns=1;i=55): structured DataType with required/optional fields.
        //   - DerivedStruct   (ns=1;i=56): structured subtype declaring one additional field.
        private const string Nodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/DataTypeTest/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/DataTypeTest/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/" Version="1.05.02" PublicationDate="2022-10-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasModellingRule">i=37</Alias>
                <Alias Alias="HasTypeDefinition">i=40</Alias>
                <Alias Alias="HasSubtype">i=45</Alias>
                <Alias Alias="Structure">i=22</Alias>
                <Alias Alias="HasProperty">i=46</Alias>
                <Alias Alias="HasComponent">i=47</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:WidgetType">
                <References>
                  <Reference ReferenceType="HasComponent">ns=1;i=10</Reference>
                </References>
              </UAObjectType>
              <UAVariable NodeId="ns=1;i=10" BrowseName="1:Mode" ParentNodeId="ns=1;i=1" DataType="ns=1;i=50">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                </References>
              </UAVariable>
              <UAObject NodeId="ns=1;i=200" BrowseName="1:WidgetInstance">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=1</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=201</Reference>
                </References>
              </UAObject>
              <UAVariable NodeId="ns=1;i=201" BrowseName="1:Reason" ParentNodeId="ns=1;i=200" DataType="ns=1;i=51">
                <References>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                </References>
              </UAVariable>
              <UADataType NodeId="ns=1;i=50" BrowseName="1:ReferencedEnum">
                <DisplayName>ReferencedEnum</DisplayName>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                </References>
                <Definition Name="1:ReferencedEnum">
                  <Field Name="Off" Value="0" />
                  <Field Name="On" Value="1" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=51" BrowseName="1:InstanceOnlyEnum">
                <DisplayName>InstanceOnlyEnum</DisplayName>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                </References>
                <Definition Name="1:InstanceOnlyEnum">
                  <Field Name="Unknown" Value="0" />
                  <Field Name="Manual" Value="1" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=52" BrowseName="1:OrphanEnum">
                <DisplayName>OrphanEnum</DisplayName>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                </References>
                <Definition Name="1:OrphanEnum">
                  <Field Name="Red" Value="0" />
                  <Field Name="Green" Value="1" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=53" BrowseName="1:WidgetStruct">
                <DisplayName>WidgetStruct</DisplayName>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=22</Reference>
                </References>
                <Definition Name="1:WidgetStruct">
                  <Field Name="Count" DataType="i=6" />
                  <Field Name="Label" DataType="i=12" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=54" BrowseName="1:TemperatureType">
                <DisplayName>TemperatureType</DisplayName>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=11</Reference>
                </References>
              </UADataType>
              <UADataType NodeId="ns=1;i=55" BrowseName="1:BaseStruct">
                <DisplayName>BaseStruct</DisplayName>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">Structure</Reference>
                </References>
                <Definition Name="1:BaseStruct">
                  <Field Name="BaseRequired" DataType="i=6" />
                  <Field Name="BaseOptional" DataType="i=12" IsOptional="true" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=56" BrowseName="1:DerivedStruct">
                <DisplayName>DerivedStruct</DisplayName>
                <References>
                  <Reference ReferenceType="i=45" IsForward="false">ns=1;i=55</Reference>
                </References>
                <Definition Name="1:DerivedStruct">
                  <Field Name="DerivedField" DataType="i=1" />
                </Definition>
              </UADataType>
            </UANodeSet>
            """;

        // A companion spec that declares an ObjectType but no DataTypes of its own.
        private const string NoDataTypeModelUri = "http://opcfoundation.org/UA/NoDataTypeTest/";

        private const string NoDataTypeNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/NoDataTypeTest/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/NoDataTypeTest/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/" Version="1.05.02" PublicationDate="2022-10-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasComponent">i=47</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:PlainType" />
            </UANodeSet>
            """;

        private const string DataTypeOnlyModelUri = "http://opcfoundation.org/UA/DataTypeOnly/";

        private const string DataTypeOnlyNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/DataTypeOnly/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/DataTypeOnly/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z" />
              </Models>
              <Aliases>
                <Alias Alias="HasSubtype">i=45</Alias>
              </Aliases>
              <UADataType NodeId="ns=1;i=1" BrowseName="1:OnlyEnum">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                </References>
                <Definition Name="1:OnlyEnum">
                  <Field Name="First" Value="0" />
                  <Field Name="Second" Value="1" />
                </Definition>
              </UADataType>
            </UANodeSet>
            """;

        private const string CoreDataTypeNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/" Version="1.05.04" PublicationDate="2025-01-01T00:00:00Z" />
              </Models>
              <Aliases>
                <Alias Alias="HasSubtype">i=45</Alias>
              </Aliases>
              <UADataType NodeId="i=1" BrowseName="Boolean">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=24</Reference>
                </References>
              </UADataType>
              <UADataType NodeId="i=27" BrowseName="Integer">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=6</Reference>
                </References>
              </UADataType>
            </UANodeSet>
            """;

        private const string LeafModelUri = "http://opcfoundation.org/UA/DataTypeLeaf/";
        private const string MiddleModelUri = "http://opcfoundation.org/UA/DataTypeMiddle/";
        private const string RootModelUri = "http://opcfoundation.org/UA/DataTypeRoot/";
        private const string BaseStructureModelUri = "http://opcfoundation.org/UA/BaseStructure/";
        private const string DerivedStructureModelUri = "http://opcfoundation.org/UA/DerivedStructure/";

        private const string BaseStructureNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/BaseStructure/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/BaseStructure/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z" />
              </Models>
              <Aliases>
              </Aliases>
              <UADataType NodeId="ns=1;i=2" BrowseName="1:BaseValueType">
                <References>
                  <Reference ReferenceType="i=45" IsForward="false">i=12</Reference>
                </References>
              </UADataType>
              <UADataType NodeId="ns=1;i=1" BrowseName="1:BaseStruct">
                <References>
                  <Reference ReferenceType="i=45" IsForward="false">i=22</Reference>
                </References>
                <Definition Name="1:BaseStruct">
                  <Field Name="BaseValue" DataType="ns=1;i=2" />
                </Definition>
              </UADataType>
            </UANodeSet>
            """;

        private const string DerivedStructureNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/DerivedStructure/</Uri>
                <Uri>http://opcfoundation.org/UA/BaseStructure/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/DerivedStructure/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/BaseStructure/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
              </Aliases>
              <UADataType NodeId="ns=1;i=1" BrowseName="1:DerivedStruct">
                <References>
                  <Reference ReferenceType="i=45" IsForward="false">ns=2;i=1</Reference>
                </References>
                <Definition Name="1:DerivedStruct">
                  <Field Name="DerivedField" DataType="i=1" />
                </Definition>
              </UADataType>
            </UANodeSet>
            """;

        private static string CreateDependencyNodeset(string modelUri, string dataTypeName, string? requiredModelUri = null)
        {
            string requiredModel = requiredModelUri == null
                ? string.Empty
                : $"<RequiredModel ModelUri=\"{requiredModelUri}\" Version=\"1.0.0\" PublicationDate=\"2025-01-01T00:00:00Z\" />";

            return $"""
                <?xml version="1.0" encoding="utf-8" ?>
                <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
                  <NamespaceUris>
                    <Uri>{modelUri}</Uri>
                  </NamespaceUris>
                  <Models>
                    <Model ModelUri="{modelUri}" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                      {requiredModel}
                    </Model>
                  </Models>
                  <Aliases>
                    <Alias Alias="HasSubtype">i=45</Alias>
                  </Aliases>
                  <UADataType NodeId="ns=1;i=1" BrowseName="1:{dataTypeName}">
                    <References>
                      <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                    </References>
                    <Definition Name="1:{dataTypeName}">
                      <Field Name="Value" Value="0" />
                    </Definition>
                  </UADataType>
                </UANodeSet>
                """;
        }

        [Fact]
        public void DataTypesModel_ContainsEveryNonBuiltInDataType()
        {
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);
            JsonElement schemaDefinitions = dataTypes.GetProperty("schemaDefinitions");

            string[] keys = schemaDefinitions.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

            Assert.Equal(
                new[] { "BaseStruct", "DerivedStruct", "InstanceOnlyEnum", "OrphanEnum", "ReferencedEnum", "TemperatureType", "WidgetStruct" },
                keys);
        }

        [Fact]
        public void InstanceOnlyEnum_IsEmittedEvenThoughNoTypeDefinitionReferencesIt()
        {
            // The "ToolLocked" scenario: an enum used only by a Variable on a concrete instance
            // must still appear as a reusable schema definition.
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);
            JsonElement schemaDefinitions = dataTypes.GetProperty("schemaDefinitions");

            Assert.True(schemaDefinitions.TryGetProperty("InstanceOnlyEnum", out JsonElement instanceOnly));
            Assert.Equal("object", instanceOnly.GetProperty("type").GetString());
            Assert.True(instanceOnly.TryGetProperty("const", out JsonElement constElt));
            Assert.Equal(0, constElt.GetProperty("Unknown").GetInt32());
            Assert.Equal(1, constElt.GetProperty("Manual").GetInt32());
        }

        [Fact]
        public void OrphanEnum_IsEmittedEvenThoughNothingReferencesIt()
        {
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);
            Assert.True(dataTypes.GetProperty("schemaDefinitions").TryGetProperty("OrphanEnum", out _));
        }

        [Fact]
        public void StructuredDataType_IsEmittedAsObjectSchema()
        {
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);
            JsonElement widgetStruct = dataTypes.GetProperty("schemaDefinitions").GetProperty("WidgetStruct");

            Assert.Equal("object", widgetStruct.GetProperty("type").GetString());
            JsonElement props = widgetStruct.GetProperty("properties");
            Assert.Equal("integer", props.GetProperty("Count").GetProperty("type").GetString());
            Assert.Equal("string", props.GetProperty("Label").GetProperty("type").GetString());
        }

        [Fact]
        public void DerivedStructuredDataType_IncludesInheritedFields()
        {
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);
            JsonElement derivedStruct = dataTypes.GetProperty("schemaDefinitions").GetProperty("DerivedStruct");
            JsonElement properties = derivedStruct.GetProperty("properties");

            Assert.Equal(new[] { "BaseOptional", "BaseRequired", "DerivedField" }, properties.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
            Assert.Equal("integer", properties.GetProperty("BaseRequired").GetProperty("type").GetString());
            Assert.Equal("string", properties.GetProperty("BaseOptional").GetProperty("type").GetString());
            Assert.Equal("boolean", properties.GetProperty("DerivedField").GetProperty("type").GetString());

            string[] required = derivedStruct.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray();
            Assert.Equal(new[] { "BaseRequired", "DerivedField" }, required);
        }

        [Fact]
        public void SimpleTypeAlias_IsEmittedWithItsPrimitiveBase()
        {
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);
            JsonElement temperature = dataTypes.GetProperty("schemaDefinitions").GetProperty("TemperatureType");

            // Subtype of Double (i=11) resolves to a JSON number.
            Assert.Equal("number", temperature.GetProperty("type").GetString());
        }

        [Fact]
        public void ReferencedEnum_RemainsInOwningTypeSchemaDefinitions()
        {
            // Regression: the legacy per-ObjectType enum extraction must be unaffected.
            JsonElement widget = GetThingByTitleSuffix(Nodeset, ModelUri, "WidgetType");
            JsonElement widgetSchemas = widget.GetProperty("schemaDefinitions");

            Assert.True(widgetSchemas.TryGetProperty("ReferencedEnum", out _));
            Assert.False(widgetSchemas.TryGetProperty("InstanceOnlyEnum", out _));
            Assert.False(widgetSchemas.TryGetProperty("OrphanEnum", out _));
        }

        [Fact]
        public void DataTypesModel_IsAValidThingModel()
        {
            JsonElement dataTypes = GetDataTypesThing(Nodeset, ModelUri);

            Assert.Equal("tm:ThingModel", dataTypes.GetProperty("@type").GetString());
            Assert.EndsWith("_DataTypes", dataTypes.GetProperty("title").GetString(), StringComparison.Ordinal);
        }

        [Fact]
        public void SpecWithNoOwnDataTypes_EmitsNoDataTypesThing()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(NoDataTypeNodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(NoDataTypeModelUri),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement[] things = doc.RootElement.EnumerateArray().Select(t => t.Clone()).ToArray();

            Assert.Contains(things, t => t.GetProperty("title").GetString() == "NoDataTypeTest_PlainType");
            Assert.DoesNotContain(things, t => t.GetProperty("title").GetString()!.EndsWith("_DataTypes", StringComparison.Ordinal));
        }

        [Fact]
        public void CoreCatalog_ExcludesOnlyBuiltInTypeDataTypes()
        {
            JsonElement dataTypes = GetDataTypesThing(CoreDataTypeNodeset, OpcUaGraph.OpcUaCoreModelUri);
            string[] keys = dataTypes.GetProperty("schemaDefinitions").EnumerateObject().Select(p => p.Name).ToArray();

            Assert.Equal(new[] { "Integer" }, keys);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DataTypeOnlyModel_EmitsCatalog(bool integrate)
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(DataTypeOnlyNodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(DataTypeOnlyModelUri),
                new LinkRelRuleEngine(),
                integrate,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement dataTypes = Assert.Single(doc.RootElement.EnumerateArray().Select(t => t.Clone()));
            Assert.True(dataTypes.GetProperty("schemaDefinitions").TryGetProperty("OnlyEnum", out _));
        }

        [Fact]
        public void IntegratedModel_EmitsTransitiveRequiredModelCatalogs()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(CreateDependencyNodeset(LeafModelUri, "LeafEnum"));
            graph.AddNodeset(CreateDependencyNodeset(MiddleModelUri, "MiddleEnum", LeafModelUri));
            graph.AddNodeset(CreateDependencyNodeset(RootModelUri, "RootEnum", MiddleModelUri));

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(RootModelUri),
                new LinkRelRuleEngine(),
                integrate: true,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            string[] schemaNames = doc.RootElement.EnumerateArray()
                .SelectMany(t => t.GetProperty("schemaDefinitions").EnumerateObject())
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(new[] { "LeafEnum", "MiddleEnum", "RootEnum" }, schemaNames);
        }

        [Fact]
        public void CrossModelDerivedStructure_PreservesFieldNamespaceAndAvoidsNodeIdCollision()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(BaseStructureNodeset);
            graph.AddNodeset(DerivedStructureNodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(DerivedStructureModelUri),
                new LinkRelRuleEngine(),
                integrate: true,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement derivedStruct = doc.RootElement.EnumerateArray()
                .SelectMany(t => t.GetProperty("schemaDefinitions").EnumerateObject())
                .Single(p => p.Name == "DerivedStruct")
                .Value;
            JsonElement properties = derivedStruct.GetProperty("properties");

            Assert.Equal("string", properties.GetProperty("BaseValue").GetProperty("type").GetString());
            Assert.Equal("boolean", properties.GetProperty("DerivedField").GetProperty("type").GetString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CommandHandler_WritesDataTypeOnlyOutput(bool integrate)
        {
            string sandboxPath = Path.Combine(Path.GetTempPath(), $"Opc2WotDataTypeTests_{Guid.NewGuid():N}");
            DirectoryInfo sandbox = Directory.CreateDirectory(sandboxPath);

            try
            {
                string nodesetPath = Path.Combine(sandbox.FullName, "DataTypeOnly.NodeSet2.xml");
                File.WriteAllText(nodesetPath, DataTypeOnlyNodeset);
                DirectoryInfo outputDir = new DirectoryInfo(Path.Combine(sandbox.FullName, "out"));

                OptionContainer options = new OptionContainer
                {
                    NodeSetsSpec = new[] { nodesetPath },
                    OutputDir = outputDir,
                    Integrate = integrate,
                    InheritVars = false,
                    IncludeTDs = false,
                };

                var errorLog = CommandHandler.ConvertSpecs(options, (_, _) => { });

                Assert.False(errorLog.HasErrors);
                Assert.Empty(errorLog.Warnings);
                FileInfo outputFile = Assert.Single(outputDir.GetFiles("*.TM.json"));
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(outputFile.FullName));
                Assert.True(Assert.Single(doc.RootElement.EnumerateArray().Select(t => t.Clone()))
                    .GetProperty("schemaDefinitions")
                    .TryGetProperty("OnlyEnum", out _));
            }
            finally
            {
                sandbox.Delete(recursive: true);
            }
        }

        private static JsonElement GetDataTypesThing(string nodeset, string modelUri)
        {
            return GetThingByTitleSuffix(nodeset, modelUri, "_DataTypes");
        }

        private static JsonElement GetThingByTitleSuffix(string nodeset, string modelUri, string titleSuffix)
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(nodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(modelUri),
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
