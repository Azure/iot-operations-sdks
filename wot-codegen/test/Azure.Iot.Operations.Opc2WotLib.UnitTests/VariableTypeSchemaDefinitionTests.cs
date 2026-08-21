// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.Opc2Wot;
    using Xunit;

    public class VariableTypeSchemaDefinitionTests
    {
        private const string ModelUri = "http://opcfoundation.org/UA/VariableTypeTest/";
        private const string BaseModelUri = "http://opcfoundation.org/UA/VariableTypeBase/";
        private const string RootModelUri = "http://opcfoundation.org/UA/VariableTypeRoot/";
        private const string BuiltInWithoutCoreModelUri = "http://opcfoundation.org/UA/BuiltInWithoutCore/";

        private const string CoreNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/" Version="1.05.04" PublicationDate="2025-01-01T00:00:00Z" />
              </Models>
              <Aliases>
                <Alias Alias="HasSubtype">i=45</Alias>
              </Aliases>
              <UADataType NodeId="i=1" BrowseName="Boolean" />
              <UADataType NodeId="i=6" BrowseName="Int32" />
              <UADataType NodeId="i=12" BrowseName="String" />
              <UAVariableType NodeId="i=62" BrowseName="BaseVariableType" IsAbstract="true" ValueRank="-2" />
              <UAVariableType NodeId="i=63" BrowseName="BaseDataVariableType" ValueRank="-2">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=62</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="i=68" BrowseName="PropertyType" ValueRank="-2">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=62</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="i=2372" BrowseName="DiscreteItemType" IsAbstract="true" ValueRank="-2">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=63</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="i=2373" BrowseName="TwoStateDiscreteType" DataType="i=1" ValueRank="-2">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=2372</Reference>
                </References>
              </UAVariableType>
            </UANodeSet>
            """;

        private const string Nodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/VariableTypeTest/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/VariableTypeTest/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/" Version="1.05.04" PublicationDate="2025-01-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasComponent">i=47</Alias>
                <Alias Alias="HasModellingRule">i=37</Alias>
                <Alias Alias="HasSubtype">i=45</Alias>
                <Alias Alias="HasTypeDefinition">i=40</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:ContainerType">
                <References>
                  <Reference ReferenceType="HasComponent">ns=1;i=10</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=11</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=12</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=13</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=14</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=15</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=16</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=17</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=18</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=19</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=20</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=21</Reference>
                  <Reference ReferenceType="HasComponent">ns=1;i=22</Reference>
                </References>
              </UAObjectType>
              <UAObject NodeId="ns=1;i=2" BrowseName="1:Container">
                <References>
                  <Reference ReferenceType="HasComponent">ns=1;i=30</Reference>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=1</Reference>
                </References>
              </UAObject>
              <UAVariable NodeId="ns=1;i=30" BrowseName="1:UIElement" ParentNodeId="ns=1;i=2">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=100</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=2</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=10" BrowseName="1:UIElement" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=100</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=11" BrowseName="1:Samples" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=101</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=12" BrowseName="1:InheritedSamples" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=102</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=13" BrowseName="1:BuiltIn" ParentNodeId="ns=1;i=1" DataType="i=6">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=68</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=14" BrowseName="1:SpecializedUIElement" ParentNodeId="ns=1;i=1" DataType="i=6">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=100</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=15" BrowseName="1:ScalarSample" ParentNodeId="ns=1;i=1" DataType="i=12" ValueRank="0">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=1;i=101</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=16" BrowseName="1:CoreTwoState" ParentNodeId="ns=1;i=1" DataType="i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=2373</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=17" BrowseName="1:CoreBaseData" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=63</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=18" BrowseName="1:CoreProperty" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=68</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=19" BrowseName="1:StructuredProperty" ParentNodeId="ns=1;i=1" DataType="ns=1;i=200">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=68</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=20" BrowseName="1:EnumProperty" ParentNodeId="ns=1;i=1" DataType="ns=1;i=201">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=68</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=21" BrowseName="1:StructuredArrayProperty" ParentNodeId="ns=1;i=1" DataType="ns=1;i=200" ValueRank="1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=68</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UAVariable NodeId="ns=1;i=22" BrowseName="1:SubtypeProperty" ParentNodeId="ns=1;i=1" DataType="ns=1;i=202">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=68</Reference>
                  <Reference ReferenceType="HasModellingRule">i=78</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
              <UADataType NodeId="ns=1;i=200" BrowseName="1:StructuredValueType">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=22</Reference>
                </References>
                <Definition Name="1:StructuredValueType">
                  <Field Name="Name" DataType="i=12" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=201" BrowseName="1:ValueKind">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                </References>
                <Definition Name="1:ValueKind">
                  <Field Name="First" Value="0" />
                  <Field Name="Second" Value="1" />
                </Definition>
              </UADataType>
              <UADataType NodeId="ns=1;i=202" BrowseName="1:AbstractWeightType">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=12</Reference>
                </References>
              </UADataType>
              <UAVariableType NodeId="ns=1;i=100" BrowseName="1:UIElementType" IsAbstract="true">
                <DisplayName>UIElementType</DisplayName>
                <Description>Semantic UI element value.</Description>
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=63</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="ns=1;i=101" BrowseName="1:SampleArrayType" DataType="i=12" ValueRank="1">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=63</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="ns=1;i=102" BrowseName="1:DerivedSampleArrayType">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">ns=1;i=101</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="ns=1;i=103" BrowseName="1:OrphanType" DataType="i=1">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=63</Reference>
                </References>
              </UAVariableType>
              <UAVariableType NodeId="ns=1;i=104" BrowseName="1:DeprecatedType" DataType="i=12" ReleaseStatus="Deprecated">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=63</Reference>
                </References>
              </UAVariableType>
            </UANodeSet>
            """;

        private const string BaseModelNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/VariableTypeBase/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/VariableTypeBase/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/" Version="1.05.04" PublicationDate="2025-01-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasSubtype">i=45</Alias>
              </Aliases>
              <UAVariableType NodeId="ns=1;i=200" BrowseName="1:SharedValueType" DataType="ns=1;i=300">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=63</Reference>
                </References>
              </UAVariableType>
              <UADataType NodeId="ns=1;i=300" BrowseName="1:SharedValueEnum">
                <References>
                  <Reference ReferenceType="HasSubtype" IsForward="false">i=29</Reference>
                </References>
                <Definition Name="1:SharedValueEnum">
                  <Field Name="First" Value="0" />
                  <Field Name="Second" Value="1" />
                </Definition>
              </UADataType>
            </UANodeSet>
            """;

        private const string RootModelNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/VariableTypeRoot/</Uri>
                <Uri>http://opcfoundation.org/UA/VariableTypeBase/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/VariableTypeRoot/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z">
                  <RequiredModel ModelUri="http://opcfoundation.org/UA/VariableTypeBase/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z" />
                </Model>
              </Models>
              <Aliases>
                <Alias Alias="HasComponent">i=47</Alias>
                <Alias Alias="HasTypeDefinition">i=40</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:RootType">
                <References>
                  <Reference ReferenceType="HasComponent">ns=1;i=2</Reference>
                </References>
              </UAObjectType>
              <UAVariable NodeId="ns=1;i=2" BrowseName="1:SharedValue" ParentNodeId="ns=1;i=1">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">ns=2;i=200</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
            </UANodeSet>
            """;

        private const string VariableTypeOnlyNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/VariableTypeOnly/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/VariableTypeOnly/" Version="1.0.0" PublicationDate="2025-01-01T00:00:00Z" />
              </Models>
              <Aliases />
              <UAVariableType NodeId="ns=1;i=1" BrowseName="1:OnlyType" DataType="i=12" />
            </UANodeSet>
            """;

        private const string BuiltInWithoutCoreNodeset = """
            <?xml version="1.0" encoding="utf-8" ?>
            <UANodeSet xmlns="http://opcfoundation.org/UA/2011/03/UANodeSet.xsd">
              <NamespaceUris>
                <Uri>http://opcfoundation.org/UA/BuiltInWithoutCore/</Uri>
              </NamespaceUris>
              <Models>
                <Model ModelUri="http://opcfoundation.org/UA/BuiltInWithoutCore/" Version="1.0.0" />
              </Models>
              <Aliases>
                <Alias Alias="HasComponent">i=47</Alias>
                <Alias Alias="HasTypeDefinition">i=40</Alias>
              </Aliases>
              <UAObjectType NodeId="ns=1;i=1" BrowseName="1:ContainerType">
                <References>
                  <Reference ReferenceType="HasComponent">ns=1;i=2</Reference>
                </References>
              </UAObjectType>
              <UAVariable NodeId="ns=1;i=2" BrowseName="1:Text" ParentNodeId="ns=1;i=1" DataType="i=12">
                <References>
                  <Reference ReferenceType="HasTypeDefinition">i=63</Reference>
                  <Reference ReferenceType="HasComponent" IsForward="false">ns=1;i=1</Reference>
                </References>
              </UAVariable>
            </UANodeSet>
            """;

        [Fact]
        public void ReferencedVariableTypes_AreLocalSchemasAndAffordanceReferences()
        {
            JsonElement thing = GetThingByTitleSuffix("ContainerType");
            JsonElement schemas = thing.GetProperty("schemaDefinitions");

            Assert.True(schemas.TryGetProperty("UIElementType", out JsonElement uiElementType));
            Assert.Equal("org.opcfoundation.UA.VariableTypeTest.UIElementType", uiElementType.GetProperty("dov:typeRef").GetString());
            Assert.Equal("string", uiElementType.GetProperty("type").GetString());

            JsonElement properties = thing.GetProperty("properties");
            Assert.Equal("#/schemaDefinitions/UIElementType", properties.GetProperty("UIElement").GetProperty("tm:ref").GetString());
            Assert.False(properties.GetProperty("UIElement").TryGetProperty("type", out _));
            Assert.Equal("integer", properties.GetProperty("BuiltIn").GetProperty("type").GetString());
            Assert.False(properties.GetProperty("BuiltIn").TryGetProperty("tm:ref", out _));
            Assert.False(properties.GetProperty("BuiltIn").TryGetProperty("dov:typeRef", out _));

            Assert.Equal(
                "#/schemaDefinitions/TwoStateDiscreteType",
                properties.GetProperty("CoreTwoState").GetProperty("tm:ref").GetString());
            Assert.Equal(
                "#/schemaDefinitions/BaseDataVariableType",
                properties.GetProperty("CoreBaseData").GetProperty("tm:ref").GetString());
            Assert.Equal(
                "#/schemaDefinitions/PropertyType",
                properties.GetProperty("CoreProperty").GetProperty("tm:ref").GetString());

            JsonElement twoStateDiscreteType = schemas.GetProperty("TwoStateDiscreteType");
            Assert.Equal("boolean", twoStateDiscreteType.GetProperty("type").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.TwoStateDiscreteType",
                twoStateDiscreteType.GetProperty("dov:typeRef").GetString());

            JsonElement specialized = properties.GetProperty("SpecializedUIElement");
            Assert.False(specialized.TryGetProperty("tm:ref", out _));
            Assert.Equal("integer", specialized.GetProperty("type").GetString());
            Assert.False(specialized.TryGetProperty("dov:typeRef", out _));

            JsonElement scalarSample = properties.GetProperty("ScalarSample");
            Assert.False(scalarSample.TryGetProperty("tm:ref", out _));
            Assert.Equal("string", scalarSample.GetProperty("type").GetString());
            Assert.False(scalarSample.TryGetProperty("dov:typeRef", out _));

            JsonElement structuredProperty = properties.GetProperty("StructuredProperty");
            Assert.False(structuredProperty.TryGetProperty("tm:ref", out _));
            Assert.Equal("object", structuredProperty.GetProperty("type").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.StructuredValueType",
                structuredProperty.GetProperty("dov:typeRef").GetString());

            JsonElement enumProperty = properties.GetProperty("EnumProperty");
            Assert.False(enumProperty.TryGetProperty("tm:ref", out _));
            Assert.Equal("integer", enumProperty.GetProperty("type").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.ValueKind",
                enumProperty.GetProperty("dov:typeRef").GetString());

            JsonElement structuredArrayProperty = properties.GetProperty("StructuredArrayProperty");
            Assert.False(structuredArrayProperty.TryGetProperty("tm:ref", out _));
            Assert.False(structuredArrayProperty.TryGetProperty("dov:typeRef", out _));
            Assert.Equal("array", structuredArrayProperty.GetProperty("type").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.StructuredValueType",
                structuredArrayProperty.GetProperty("items").GetProperty("dov:typeRef").GetString());

            JsonElement events = thing.GetProperty("events");
            Assert.Equal("#/schemaDefinitions/UIElementType", events.GetProperty("UIElement").GetProperty("data").GetProperty("tm:ref").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.StructuredValueType",
                events.GetProperty("StructuredProperty").GetProperty("data").GetProperty("dov:typeRef").GetString());
            Assert.False(
                events.GetProperty("BuiltIn").GetProperty("data").TryGetProperty("dov:typeRef", out _));
        }

        [Fact]
        public void VariableTypeValueRankAndDataType_AreInherited()
        {
            JsonElement schemas = GetThingByTitleSuffix("ContainerType").GetProperty("schemaDefinitions");

            JsonElement directArray = schemas.GetProperty("SampleArrayType");
            Assert.Equal("array", directArray.GetProperty("type").GetString());
            Assert.Equal("string", directArray.GetProperty("items").GetProperty("type").GetString());
            Assert.Equal("org.opcfoundation.UA.VariableTypeTest.SampleArrayType", directArray.GetProperty("dov:typeRef").GetString());

            JsonElement inheritedArray = schemas.GetProperty("DerivedSampleArrayType");
            Assert.Equal("array", inheritedArray.GetProperty("type").GetString());
            Assert.Equal("string", inheritedArray.GetProperty("items").GetProperty("type").GetString());
            Assert.Equal("org.opcfoundation.UA.VariableTypeTest.DerivedSampleArrayType", inheritedArray.GetProperty("dov:typeRef").GetString());
        }

        [Fact]
        public void SpecializedSubtypeDataType_RetainsDataTypeReference()
        {
            JsonElement thing = GetThingByTitleSuffix("ContainerType");
            JsonElement subtypeProperty = thing.GetProperty("properties").GetProperty("SubtypeProperty");

            Assert.Equal("string", subtypeProperty.GetProperty("type").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.AbstractWeightType",
                subtypeProperty.GetProperty("dov:typeRef").GetString());

            JsonElement subtypeEventData = thing.GetProperty("events").GetProperty("SubtypeProperty").GetProperty("data");
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.AbstractWeightType",
                subtypeEventData.GetProperty("dov:typeRef").GetString());
        }

        [Fact]
        public void BuiltInDataType_DoesNotRequireCoreNodeset()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(BuiltInWithoutCoreNodeset);

            OpcUaVariable variable = graph.GetOpcUaModelInfo(BuiltInWithoutCoreModelUri)
                .NodeIdToObjectTypeMap
                .Values
                .Single()
                .VariableRecords
                .Values
                .Single()
                .UaVariable;

            Assert.Null(variable.CustomDataTypeSubtype);
        }

        [Fact]
        public void ThingDescription_ExactVariableTypeRetainsTypeReference()
        {
            OpcUaGraph graph = CreateGraph();
            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(ModelUri),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: true);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement thingDescription = doc.RootElement.EnumerateArray()
                .Single(t => t.GetProperty("title").GetString() == "VariableTypeTest_Container");
            JsonElement property = thingDescription.GetProperty("properties").GetProperty("UIElement");

            Assert.Equal("string", property.GetProperty("type").GetString());
            Assert.Equal(
                "org.opcfoundation.UA.VariableTypeTest.UIElementType",
                property.GetProperty("dov:typeRef").GetString());
        }

        [Fact]
        public void VariableTypeCatalog_ContainsAllNonDeprecatedCompanionTypes()
        {
            JsonElement catalog = GetThingByTitleSuffix("_VariableTypes");
            string[] schemaNames = catalog.GetProperty("schemaDefinitions")
                .EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[] { "DerivedSampleArrayType", "OrphanType", "SampleArrayType", "UIElementType" },
                schemaNames);
        }

        [Fact]
        public void CoreVariableTypeCatalog_ContainsAbstractAndConcreteTypes()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(CoreNodeset);
            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(OpcUaGraph.OpcUaCoreModelUri),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement catalog = Assert.Single(doc.RootElement.EnumerateArray().Select(t => t.Clone()));
            Assert.Equal("OpcUaCore_VariableTypes", catalog.GetProperty("title").GetString());

            string[] schemaNames = catalog.GetProperty("schemaDefinitions")
                .EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[] { "BaseDataVariableType", "BaseVariableType", "DiscreteItemType", "PropertyType", "TwoStateDiscreteType" },
                schemaNames);
        }

        [Fact]
        public void IntegratedOutput_IncludesRequiredVariableTypeCatalogs()
        {
            OpcUaGraph graph = CreateGraph();
            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(ModelUri),
                new LinkRelRuleEngine(),
                integrate: true,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            string[] catalogTitles = doc.RootElement.EnumerateArray()
                .Select(t => t.GetProperty("title").GetString()!)
                .Where(t => t.EndsWith("_VariableTypes", StringComparison.Ordinal))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "OpcUaCore_VariableTypes", "VariableTypeTest_VariableTypes" }, catalogTitles);
        }

        [Fact]
        public void IntegratedOutput_IncludesRequiredCompanionCatalogAndLocalReference()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(CoreNodeset);
            graph.AddNodeset(BaseModelNodeset);
            graph.AddNodeset(RootModelNodeset);

            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(RootModelUri),
                new LinkRelRuleEngine(),
                integrate: true,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            JsonElement rootThing = doc.RootElement.EnumerateArray()
                .Single(t => t.GetProperty("title").GetString() == "VariableTypeRoot_RootType");
            Assert.Equal(
                "#/schemaDefinitions/SharedValueType",
                rootThing.GetProperty("properties").GetProperty("SharedValue").GetProperty("tm:ref").GetString());
            Assert.True(rootThing.GetProperty("schemaDefinitions").TryGetProperty("SharedValueType", out _));
            Assert.Equal(
                "integer",
                rootThing.GetProperty("schemaDefinitions").GetProperty("SharedValueType").GetProperty("type").GetString());
            Assert.Equal(
                "SharedValueEnum",
                rootThing.GetProperty("schemaDefinitions").GetProperty("SharedValueType").GetProperty("title").GetString());

            Assert.Contains(
                doc.RootElement.EnumerateArray(),
                t => t.GetProperty("title").GetString() == "VariableTypeBase_VariableTypes");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CommandHandler_WritesVariableTypeOnlyOutput(bool integrate)
        {
            string sandboxPath = Path.Combine(Path.GetTempPath(), $"Opc2WotVariableTypeTests_{Guid.NewGuid():N}");
            DirectoryInfo sandbox = Directory.CreateDirectory(sandboxPath);

            try
            {
                string nodesetPath = Path.Combine(sandbox.FullName, "VariableTypeOnly.NodeSet2.xml");
                File.WriteAllText(nodesetPath, VariableTypeOnlyNodeset);
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
                Assert.Equal("VariableTypeOnly_OnlyType.TM.json", outputFile.Name);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(outputFile.FullName));
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
                JsonElement catalog = doc.RootElement;
                Assert.Equal("VariableTypeOnly_OnlyType", catalog.GetProperty("title").GetString());
                Assert.True(catalog.GetProperty("schemaDefinitions").TryGetProperty("OnlyType", out _));
            }
            finally
            {
                sandbox.Delete(recursive: true);
            }
        }

        private static JsonElement GetThingByTitleSuffix(string titleSuffix)
        {
            OpcUaGraph graph = CreateGraph();
            WotThingCollection collection = new WotThingCollection(
                graph,
                graph.GetOpcUaModelInfo(ModelUri),
                new LinkRelRuleEngine(),
                integrate: false,
                inheritVars: false,
                includeTDs: false);

            using JsonDocument doc = JsonDocument.Parse(collection.TransformText());
            return doc.RootElement.EnumerateArray()
                .Single(t => t.GetProperty("title").GetString()!.EndsWith(titleSuffix, StringComparison.Ordinal))
                .Clone();
        }

        private static OpcUaGraph CreateGraph()
        {
            OpcUaGraph graph = new OpcUaGraph();
            graph.AddNodeset(CoreNodeset);
            graph.AddNodeset(Nodeset);
            return graph;
        }
    }
}
