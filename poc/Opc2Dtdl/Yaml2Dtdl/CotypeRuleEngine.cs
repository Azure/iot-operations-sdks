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
        private int matchedCount;
        private int unmatchedCount;
        private int deliberateNullCount;

        public CotypeRuleEngine(Dictionary<string, List<string>> objectTypeSupers, string rulesPath)
        {
            matchedCount = 0;
            unmatchedCount = 0;
            deliberateNullCount = 0;

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

        public void DisplayStats()
        {
            Console.WriteLine($"Relationships with matched co-type:          {matchedCount}");
            Console.WriteLine($"Relationships with unmatched co-type:        {unmatchedCount}");
            Console.WriteLine($"Relationships intentionally without co-type: {deliberateNullCount}");
        }

        public string? GetCotype(OpcUaDefinedType sourceDefinedType, OpcUaDefinedType relationshipDefinedType, OpcUaDefinedType? targetDefinedType)
        {
            foreach (CotypeRule cotypeRule in cotypeRules)
            {
                if (cotypeRule.TryMatchRule(sourceDefinedType, relationshipDefinedType, targetDefinedType, out string? matchingCotype))
                {
                    if (matchingCotype != null)
                    {
                        matchedCount++;
                    }
                    else
                    {
                        deliberateNullCount++;
                    }

                    return matchingCotype;
                }
            }

            unmatchedCount++;
            return null;
        }
    }
}
