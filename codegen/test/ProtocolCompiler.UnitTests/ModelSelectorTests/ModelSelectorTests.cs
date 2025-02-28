namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.ModelSelectorTests
{
    using System;
    using DTDLParser;
    using Azure.Iot.Operations.ProtocolCompiler;

    public class ModelSelectorTests
    {
        private const string rootPath = "../../../ModelSelectorTests";
        private const string modelsPath = $"{rootPath}/models";

        private static readonly Uri dmrPath = new Uri(Path.GetFullPath($"{rootPath}/dmr"));
        private static readonly Uri nonExistentPath = new Uri(Path.GetFullPath($"{rootPath}/nonexistent"));
        private static readonly Uri dmrUri = new Uri("https://devicemodels.azure.com/");
        private static readonly Uri nonExistentUri = new Uri("https://not.exist.azure.com/");

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
                null,
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
                null,
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
                null,
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
                null,
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
                null,
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
                null,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefMissingFileRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsBaseText },
                new string[] { "Extends" },
                null,
                nonExistentPath,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefNotInFileRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsDIText },
                new string[] { "Extends" },
                null,
                dmrPath,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefInFileRepo_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsBaseText },
                new string[] { "Extends" },
                null,
                dmrPath,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefMissingRemoteRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsDIText },
                new string[] { "Extends" },
                null,
                nonExistentUri,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact(Skip = "global DMR is planned to be archived")]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefNotInRemoteRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsBaseText },
                new string[] { "Extends" },
                null,
                dmrUri,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact(Skip = "global DMR is planned to be archived")]
        public async Task ModelTextWithOneCotypedInterfaceExternalRefInRemoteRepo_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { interfaceExtendsDIText },
                new string[] { "Extends" },
                null,
                dmrUri,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task NoModelTextMissingFileRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiExtendsBase,
                nonExistentPath,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task NoModelTextDtmiNotInFileRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiMqttAlpha,
                dmrPath,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task NoModelTextDtmiInFileRepoNoCotype_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiInterfaceBase,
                dmrPath,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task NoModelTextDtmiInFileRepoWithCotype_Succeeds()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiExtendsBase,
                dmrPath,
                _ => { });

            Assert.NotNull(contextualizedInterface.InterfaceId);
        }

        [Fact]
        public async Task NoModelTextMissingRemoteRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiExtendsBase,
                nonExistentUri,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact(Skip = "global DMR is planned to be archived")]
        public async Task NoModelTextDtmiNotInRemoteRepo_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiMqttAlpha,
                dmrUri,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }

        [Fact(Skip = "global DMR is planned to be archived")]
        public async Task NoModelTextDtmiInRemoteRepoNoCotype_Fails()
        {
            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(
                new string[] { },
                new string[] { },
                dtmiDeviceInformation,
                dmrUri,
                _ => { });

            Assert.Null(contextualizedInterface.InterfaceId);
        }
    }
}
