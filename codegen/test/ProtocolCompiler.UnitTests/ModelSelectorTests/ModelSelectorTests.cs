namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.ModelSelectorTests
{
    using DTDLParser;
    using Azure.Iot.Operations.ProtocolCompiler;

    public class ModelSelectorTests
    {
        private const string rootPath = "../../../ModelSelectorTests";
        private const string modelsPath = $"{rootPath}/models";

        private static readonly Dtmi dtmiNoCotype = new Dtmi("dtmi:akri:DTDL:ModelSelector:noCotype;1");
        private static readonly Dtmi dtmiMqttAlpha = new Dtmi("dtmi:akri:DTDL:ModelSelector:mqttAlpha;1");
        private static readonly Dtmi dtmiMqttAlphaTelem = new Dtmi("dtmi:akri:DTDL:ModelSelector:mqttAlpha:_contents:__alpha;1");
        private static readonly Dtmi dtmiMqttBeta = new Dtmi("dtmi:akri:DTDL:ModelSelector:mqttBeta;1");
        private static readonly Dtmi dtmiExtendsBase = new Dtmi("dtmi:akri:DTDL:ModelSelector:extendsBase;1");
        private static readonly Dtmi dtmiExtendsDI = new Dtmi("dtmi:akri:DTDL:ModelSelector:extendsDI;1");
        private static readonly Dtmi dtmiInterfaceBase = new Dtmi("dtmi:akri:DTDL:ModelSelector:interfaceBase;1");
        private static readonly Dtmi dtmiDeviceInformation = new Dtmi("dtmi:azure:DeviceManagement:DeviceInformation;1");

        private static readonly string interfaceNoCotypeText = File.OpenText($"{modelsPath}/InterfaceNoCotype.json").ReadToEnd();
        private static readonly string interfaceMqttAlphaText = File.OpenText($"{modelsPath}/InterfaceMqttAlpha.json").ReadToEnd();
        private static readonly string interfaceMqttBetaText = File.OpenText($"{modelsPath}/InterfaceMqttBeta.json").ReadToEnd();
        private static readonly string interfaceExtendsBaseText = File.OpenText($"{modelsPath}/InterfaceExtendsBase.json").ReadToEnd();
        private static readonly string interfaceExtendsDIText = File.OpenText($"{modelsPath}/InterfaceExtendsDI.json").ReadToEnd();

        [Fact]
        public async Task ModelTextWithOneInterfaceNoCotype_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceNoCotypeText },
                new string[] { "NoCotype" },
                null,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithOneCotypedInterface_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceMqttAlphaText },
                new string[] { "Alpha" },
                null,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoInterfacesOneHasCotypeNoIdentification_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceNoCotypeText, interfaceMqttAlphaText },
                new string[] { "NoCotype", "Alpha" },
                null,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoCotypedInterfacesNoIdentification_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceMqttAlphaText, interfaceMqttBetaText },
                new string[] { "Alpha", "Beta" },
                null,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoInterfacesOneHasCotypeDtmiIdentifiedNotInModel_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceNoCotypeText, interfaceMqttAlphaText },
                new string[] { "NoCotype", "Alpha" },
                dtmiMqttBeta,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoInterfacesOneHasCotypeIdentifiedNonInterface_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceNoCotypeText, interfaceMqttAlphaText },
                new string[] { "NoCotype", "Alpha" },
                dtmiMqttAlphaTelem,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoInterfacesOneHasCotypeOtherIdentified_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceNoCotypeText, interfaceMqttAlphaText },
                new string[] { "NoCotype", "Alpha" },
                dtmiNoCotype,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoInterfacesOneHasCotypeIdentified_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceNoCotypeText, interfaceMqttAlphaText },
                new string[] { "NoCotype", "Alpha" },
                dtmiMqttAlpha,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithTwoCotypedInterfacesOneIdentified_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceMqttAlphaText, interfaceMqttBetaText },
                new string[] { "Alpha", "Beta" },
                dtmiMqttAlpha,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefNoRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsBaseText },
                new string[] { "Extends" },
                null,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }
    }
}
