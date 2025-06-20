namespace Yaml2Dtdl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using OpcUaDigest;

    public class CotypeRuleEngine
    {
        private List<CotypeRule> cotypeRules;

        public CotypeRuleEngine(Dictionary<string, List<string>> objectTypeSupers, string rulesPath)
        {
            if (!File.Exists(rulesPath))
            {
                throw new Exception($"Cotype rules file {rulesPath} not found");
            }

            using (StreamReader configReader = File.OpenText(rulesPath))
            {
                using (JsonDocument configDoc = JsonDocument.Parse(configReader.ReadToEnd()))
                {
                    cotypeRules = configDoc.RootElement.EnumerateArray().Select(e => new CotypeRule(e, objectTypeSupers)).ToList();
                }
            }
        }

        public void Display()
        {
            foreach (CotypeRule cotypeRule in cotypeRules)
            {
                Console.WriteLine();
                cotypeRule.Display();
            }
        }

        public string? GetCotype(OpcUaDefinedType sourceDefinedType, OpcUaDefinedType relationshipDefinedType, OpcUaDefinedType? targetDefinedType)
        {
            foreach (CotypeRule cotypeRule in cotypeRules)
            {
                if (cotypeRule.TryMatchRule(sourceDefinedType, relationshipDefinedType, targetDefinedType, out string? matchingCotype))
                {
                    return matchingCotype;
                }
            }

            return null;
        }
    }
}
