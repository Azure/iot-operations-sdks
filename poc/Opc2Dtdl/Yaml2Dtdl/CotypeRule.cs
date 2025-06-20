namespace Yaml2Dtdl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using OpcUaDigest;

    public class CotypeRule
    {
        private readonly Dictionary<string, List<string>> objectTypeIdSupers;
        private readonly List<string>? modellingRules;
        private readonly List<string>? sources;
        private readonly List<string>? sourcesExact;
        private readonly List<string>? targets;
        private readonly List<string>? targetsExact;
        private readonly string? cotype;

        public CotypeRule(JsonElement ruleElt, Dictionary<string, List<string>> objectTypeIdSupers)
        {
            this.objectTypeIdSupers = objectTypeIdSupers;

            modellingRules = GetValues("modellingRule", ruleElt)?.Select(s => ((int)Enum.Parse<ModellingRule>(s)).ToString())?.ToList();

            sources = GetValues("source", ruleElt);
            sourcesExact = GetValues("sourceExact", ruleElt);

            targets = GetValues("target", ruleElt);
            targetsExact = GetValues("targetExact", ruleElt);

            cotype = ruleElt.GetProperty("cotype").GetString();
        }

        public void Display()
        {
            Console.WriteLine($"modellingRules: {(modellingRules != null ? string.Join(',', modellingRules) : "(null)")}");
            Console.WriteLine($"sources: {(sources != null ? string.Join(',', sources) : "(null)")}");
            Console.WriteLine($"sourcesExact: {(sourcesExact != null ? string.Join(',', sourcesExact) : "(null)")}");
            Console.WriteLine($"targets: {(targets != null ? string.Join(',', targets) : "(null)")}");
            Console.WriteLine($"targetsExact: {(targetsExact != null ? string.Join(',', targetsExact) : "(null)")}");
            Console.WriteLine($"cotype: {cotype ?? "(null)"}");
        }

        public bool TryMatchRule(OpcUaDefinedType sourceDefinedType, OpcUaDefinedType relationshipDefinedType, OpcUaDefinedType? targetDefinedType, out string? matchingCotype)
        {
            if (DoesMatchModelingRule(relationshipDefinedType) &&
                DoesMatchType(sourceDefinedType, sources, exact: false) &&
                DoesMatchType(sourceDefinedType, sourcesExact, exact: true) &&
                DoesMatchType(targetDefinedType, targets, exact: false) &&
                DoesMatchType(targetDefinedType, targetsExact, exact: true))
            {
                matchingCotype = cotype;
                return true;
            }
            else
            {
                matchingCotype = null;
                return false;
            }
        }

        private bool DoesMatchModelingRule(OpcUaDefinedType definedType) =>
            modellingRules == null || modellingRules.Any(r => HasModellingRule(definedType, r));

        private bool DoesMatchType(OpcUaDefinedType? definedType, List<string>? ruleTypeIds, bool exact)
        {
            if (ruleTypeIds == null)
            {
                return true;
            }

            if (definedType == null)
            {
                return false;
            }

            string typeId = TypeConverter.GetModelId(definedType);
            return ruleTypeIds.Any(t => t == typeId || (!exact && DoesAncestorHaveType(typeId, t)));
        }

        private bool DoesAncestorHaveType(string objectTypeId, string keyTypeId)
        {
            foreach (string supertype in objectTypeIdSupers[objectTypeId])
            {
                if (supertype == keyTypeId || DoesAncestorHaveType(supertype, keyTypeId))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string>? GetValues(string propName, JsonElement ruleElt) => ruleElt.TryGetProperty(propName, out JsonElement propElt) ? GetStringOrStrings(propElt) : null;

        private static List<string> GetStringOrStrings(JsonElement elt)
        {
            return elt.ValueKind == JsonValueKind.Array ? elt.EnumerateArray().Select(e => e.GetString()!).ToList() : new List<string> { elt.GetString()! };
        }

        private static bool HasModellingRule(OpcUaDefinedType definedType, string modellingRule) =>
            definedType.Contents.Any(c => c.Relationship == "HasModellingRule" && c.DefinedType.NodeId == modellingRule);
    }
}
