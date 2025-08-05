namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DTDLParser;
    using DTDLParser.Models;

    public static class ModelSelector
    {
        public static async Task<ContextualizedInterface> GetInterfaceAndModelContext(string[] modelTexts, string[] modelNames, Dtmi? modelDtmi, DtmiResolverAsync? dtmiResolverAsync, Action<string?>? acceptHelpMessage)
        {
            ContextualizedInterface contextualizedInterface = new();

            ParsingOptions parsingOptions = new();
            parsingOptions.DtmiResolverAsync = dtmiResolverAsync;

            parsingOptions.ExtensionLimitContexts = new List<Dtmi> { new Dtmi("dtmi:dtdl:limits:onvif"), new Dtmi("dtmi:dtdl:limits:aio") };

            DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
            {
                sourceName = modelNames[parseIndex];
                sourceLine = parseLine;
                return true;
            };

            var modelParser = new ModelParser(parsingOptions);

            try
            {
                contextualizedInterface.ModelDict = !modelTexts.Any() && modelDtmi != null && dtmiResolverAsync != null ?
                    await modelParser.ParseAsync(dtmiResolverAsync(new List<Dtmi> { modelDtmi })) :
                    await modelParser.ParseAsync(EnumerableStringToAsync(modelTexts), parseLocator);
            }
            catch (ParsingException pex)
            {
                if (acceptHelpMessage == null)
                {
                    throw new Exception(pex.Errors[0].ToString());
                }

                StringBuilder errorStringBuilder = new();
                foreach (ParsingError perr in pex.Errors)
                {
                    acceptHelpMessage(perr.Message);
                }

                return contextualizedInterface;
            }
            catch (ResolutionException rex)
            {
                if (acceptHelpMessage == null)
                {
                    throw;
                }

                acceptHelpMessage(rex.Message);
                return contextualizedInterface;
            }

            if (modelDtmi != null)
            {
                if (!contextualizedInterface.ModelDict.TryGetValue(modelDtmi, out DTEntityInfo? dtEntity))
                {
                    RelayHelpMessage(acceptHelpMessage, $"{modelDtmi} not found in model");
                    return contextualizedInterface;
                }
                else if (dtEntity.EntityKind != DTEntityKind.Interface)
                {
                    RelayHelpMessage(acceptHelpMessage, $"{modelDtmi} is not an Interface");
                    return contextualizedInterface;
                }
                else if (!dtEntity.SupplementalTypes.Any(t => DtdlMqttExtensionValues.MqttAdjunctTypeRegex.IsMatch(t.AbsoluteUri)))
                {
                    RelayHelpMessage(acceptHelpMessage, $"{modelDtmi} does not have a co-type of {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.MqttAdjunctTypePattern)}");
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
                        RelayHelpMessage(acceptHelpMessage, $"No Interface in model has a co-type of {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.MqttAdjunctTypePattern)}");
                        break;
                    case 1:
                        contextualizedInterface.InterfaceId = mqttInterfaces.First().Id;
                        break;
                    default:
                        RelayHelpMessage(acceptHelpMessage, $"More than one Interface has a co-type of {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.MqttAdjunctTypePattern)}");
                        acceptHelpMessage!($"Resubmit command with one of the following options:");
                        foreach (DTInterfaceInfo mqttInterface in mqttInterfaces)
                        {
                            acceptHelpMessage!($"  --modelId {mqttInterface.Id}");
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

        private static void RelayHelpMessage(Action<string>? acceptor, string message)
        {
            if (acceptor == null)
            {
                throw new InvalidOperationException(message);
            }

            acceptor(message);
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

        public class ContextualizedInterface
        {
            public IReadOnlyDictionary<Dtmi, DTEntityInfo>? ModelDict = null;
            public Dtmi? InterfaceId = null;
            public int MqttVersion = 0;
        }
    }
}
