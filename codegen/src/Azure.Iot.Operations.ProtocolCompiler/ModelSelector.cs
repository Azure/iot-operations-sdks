namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Azure;
    using Azure.IoT.ModelsRepository;
    using DTDLParser;
    using DTDLParser.Models;

    internal static class ModelSelector
    {
        public static async Task<ContextualizedInterface> GetInterfaceAndModelContext(string[] modelTexts, string[] modelNames, Dtmi? modelDtmi, Uri? dmrUri, Action<string?> acceptHelpMessage)
        {
            ContextualizedInterface contextualizedInterface = new();

            ModelsRepositoryClient? dmrClient = null;
            ParsingOptions parsingOptions = new();

            parsingOptions.ExtensionLimitContexts = new List<Dtmi> { new Dtmi("dtmi:dtdl:limits:onvif") };

            if (dmrUri != null)
            {
                dmrClient = new ModelsRepositoryClient(dmrUri);
                parsingOptions.DtmiResolverAsync = dmrClient.ResolveAsync;
                parsingOptions.DtdlResolveLocator = (Dtmi resolveDtmi, int resolveLine, out string sourceName, out int sourceLine) =>
                {
                    sourceName = $"the definition of {resolveDtmi} retrieved from {dmrUri}";
                    sourceLine = resolveLine;
                    return true;
                };
            }

            DtdlParseLocator? parseLocator = null;

            if (modelTexts.Any())
            {
                parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
                {
                    sourceName = modelNames[parseIndex];
                    sourceLine = parseLine;
                    return true;
                };
            }
            else
            {
                ModelResult? modelDtdl = null;
                try
                {
                    modelDtdl = await dmrClient!.GetModelAsync(modelDtmi!.AbsoluteUri, ModelDependencyResolution.Disabled);
                }
                catch (RequestFailedException ex)
                {
                    acceptHelpMessage(ex.Status == 0 || ex.Status == (int)HttpStatusCode.NotFound ? $"{modelDtmi} not found in repository {dmrUri}" : $"Unable to access repository {dmrUri}");
                    return contextualizedInterface;
                }

                modelTexts = modelDtdl.Content.Values.ToArray();
                string[] modelIds = modelDtdl.Content.Keys.ToArray();

                parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
                {
                    sourceName = $"The definition of {modelIds[parseIndex]} retrieved from {dmrUri}";
                    sourceLine = parseLine;
                    return true;
                };
            }

            var modelParser = new ModelParser(parsingOptions);

            try
            {
                contextualizedInterface.ModelDict = await modelParser.ParseAsync(EnumerableStringToAsync(modelTexts), parseLocator);
            }
            catch (ParsingException pex)
            {
                StringBuilder errorStringBuilder = new();
                foreach (ParsingError perr in pex.Errors)
                {
                    acceptHelpMessage(perr.Message);
                }

                return contextualizedInterface;
            }
            catch (ResolutionException rex)
            {
                acceptHelpMessage(rex.Message);
                return contextualizedInterface;
            }

            if (modelDtmi != null)
            {
                if (!contextualizedInterface.ModelDict.TryGetValue(modelDtmi, out DTEntityInfo? dtEntity))
                {
                    acceptHelpMessage($"{modelDtmi} not found in model");
                    return contextualizedInterface;
                }
                else if (dtEntity.EntityKind != DTEntityKind.Interface)
                {
                    acceptHelpMessage($"{modelDtmi} is not an Interface");
                    return contextualizedInterface;
                }
                else if (!dtEntity.SupplementalTypes.Any(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri)))
                {
                    acceptHelpMessage($"{modelDtmi} does not have a co-type of {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.MqttAdjunctTypePattern)}");
                    return contextualizedInterface;
                }
                else
                {
                    contextualizedInterface.InterfaceId = modelDtmi;
                }
            }
            else
            {
                IEnumerable<DTInterfaceInfo> mqttInterfaces = contextualizedInterface.ModelDict.Values.Where(e => e.EntityKind == DTEntityKind.Interface && e.SupplementalTypes.Any(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri))).Select(e => (DTInterfaceInfo)e);
                switch (mqttInterfaces.Count())
                {
                    case 0:
                        acceptHelpMessage($"No Interface in model has a co-type of {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.MqttAdjunctTypePattern)}");
                        break;
                    case 1:
                        contextualizedInterface.InterfaceId = mqttInterfaces.First().Id;
                        break;
                    default:
                        acceptHelpMessage($"More than one Interface has a co-type of {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.MqttAdjunctTypePattern)}");
                        acceptHelpMessage($"Resubmit command with one of the following options:");
                        foreach (DTInterfaceInfo mqttInterface in mqttInterfaces)
                        {
                            acceptHelpMessage($"  --modelId {mqttInterface.Id}");
                        }
                        break;
                }
            }

            if (contextualizedInterface.InterfaceId != null)
            {
                Dtmi mqttTypeId = contextualizedInterface.ModelDict[contextualizedInterface.InterfaceId].SupplementalTypes.First(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
                contextualizedInterface.MqttVersion = int.Parse(DtdlMqttExtensionValues.MqttAdjunctTypeRegex.Match(mqttTypeId.AbsoluteUri).Groups[1].Captures[0].Value);
            }

            return contextualizedInterface;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<string> EnumerableStringToAsync(IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                yield return value;
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        private static async IAsyncEnumerable<string> ResolveAsync(
            this ModelsRepositoryClient dmrClient,
            IReadOnlyCollection<Dtmi> dtmis,
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var dtmi in dtmis.Select(s => s.AbsoluteUri))
            {
                ModelResult? modelResult = null;
                try
                {
                    modelResult = await dmrClient.GetModelAsync(dtmi, ModelDependencyResolution.Disabled, ct);
                }
                catch (Exception)
                {
                }

                if (modelResult != null)
                {
                    yield return modelResult.Content[dtmi];
                }
            }
        }

        public class ContextualizedInterface
        {
            public IReadOnlyDictionary<Dtmi, DTEntityInfo>? ModelDict = null;
            public Dtmi? InterfaceId = null;
            public int MqttVersion = 0;
        }
    }
}
