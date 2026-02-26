// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    public class LinkRelRuleEngine
    {
        private const string DefaultRel = "aov:reference";

        private readonly List<LinkRelRule> linkRelRules;

        public LinkRelRuleEngine()
        {
            Stream linkRulesStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.conversion.LinkRelRules.json")!;
            string linkRulesText = new StreamReader(linkRulesStream).ReadToEnd();

            using (JsonDocument linkRulesDoc = JsonDocument.Parse(linkRulesText))
            {
                linkRelRules = linkRulesDoc.RootElement.EnumerateArray().Select(e => new LinkRelRule(e)).ToList();
            }
        }

        public string GetLinkRel(OpcUaObjectType sourceObjectType, OpcUaObjectType targetObjectType, OpcUaNodeId? modellingRule)
        {
            HashSet<string> sourceAncestors = sourceObjectType.AncestorNames;
            HashSet<string> targetAncestors = targetObjectType.AncestorNames;

            foreach (LinkRelRule linkRelRule in linkRelRules)
            {
                if (linkRelRule.TryMatchRule(sourceObjectType, targetObjectType, modellingRule, sourceAncestors, targetAncestors, out string? matchingCotype))
                {
                    return matchingCotype;
                }
            }

            return DefaultRel;
        }
    }
}
