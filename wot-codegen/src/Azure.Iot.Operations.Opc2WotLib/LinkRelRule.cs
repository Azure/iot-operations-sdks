// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text.Json;

    public class LinkRelRule
    {
        private readonly List<int>? modellingRuleNodeIndices;
        private readonly List<string>? sources;
        private readonly List<string>? sourcesExact;
        private readonly List<string>? targets;
        private readonly List<string>? targetsExact;
        private readonly string rel;

        public LinkRelRule(JsonElement ruleElt)
        {
            modellingRuleNodeIndices = GetValues("modellingRule", ruleElt)?.Select(s => (int)Enum.Parse<ModellingRule>(s))?.ToList();

            sources = GetValues("source", ruleElt);
            sourcesExact = GetValues("sourceExact", ruleElt);

            targets = GetValues("target", ruleElt);
            targetsExact = GetValues("targetExact", ruleElt);

            rel = ruleElt.GetProperty("rel").GetString()!;
        }

        public bool TryMatchRule(OpcUaObjectType sourceObjectType, OpcUaObjectType targetObjectType, OpcUaNodeId? modellingRule, HashSet<string> sourceAncestors, HashSet<string> targetAncestors, [NotNullWhen(true)] out string? matchingCotype)
        {
            if (DoesMatchModelingRule(modellingRule) &&
                DoesMatchTypeRule(sources, sourceObjectType, sourceAncestors) &&
                DoesMatchTypeRule(sourcesExact, sourceObjectType) &&
                DoesMatchTypeRule(targets, targetObjectType, targetAncestors) &&
                DoesMatchTypeRule(targetsExact, targetObjectType))
            {
                matchingCotype = rel;
                return true;
            }
            else
            {
                matchingCotype = null;
                return false;
            }
        }

        private bool DoesMatchModelingRule(OpcUaNodeId? modellingRule) =>
            modellingRuleNodeIndices == null ||
            (modellingRule != null && modellingRule.NsIndex == 0 && modellingRuleNodeIndices.Any(i => i == modellingRule.NodeIndex));

        private bool DoesMatchTypeRule(List<string>? rulePatterns, OpcUaObjectType objectType, HashSet<string>? ancestors = null)
        {
            string nodeEffectiveName = WotUtil.LegalizeName(objectType.DiscriminatedEffectiveName, SpecMapper.GetSpecNameFromUri(objectType.DefiningModel.ModelUri));
            return rulePatterns == null || rulePatterns.Any(p => DoesPatternMatchName(p, nodeEffectiveName) || (ancestors != null && ancestors.Any(a => DoesPatternMatchName(p, a))));
        }

        private static bool DoesPatternMatchName(string rulePattern, string nodeEffectiveName)
        {
            if (rulePattern.Contains('*'))
            {
                int starIx = rulePattern.IndexOf("*");
                string prefix = rulePattern.Substring(0, starIx);
                string suffix = rulePattern.Substring(starIx + 1);
                return nodeEffectiveName.StartsWith(prefix) && nodeEffectiveName.EndsWith(suffix);
            }
            else
            {
                return rulePattern.Equals(nodeEffectiveName, StringComparison.Ordinal);
            }
        }

        private static List<string>? GetValues(string propName, JsonElement ruleElt) => ruleElt.TryGetProperty(propName, out JsonElement propElt) ? GetStringOrStrings(propElt) : null;

        private static List<string> GetStringOrStrings(JsonElement elt)
        {
            return elt.ValueKind == JsonValueKind.Array ? elt.EnumerateArray().Select(e => e.GetString()!).ToList() : new List<string> { elt.GetString()! };
        }
    }
}
