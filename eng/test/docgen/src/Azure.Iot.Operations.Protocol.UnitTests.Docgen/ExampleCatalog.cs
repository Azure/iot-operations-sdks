namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class ExampleCatalog
    {
        private static readonly Regex Parse = new(@"^(\s*)((?:- )?)(?:([A-Za-z0-9\-]+)\s*:)?\s*(.*)$", RegexOptions.Compiled);
        private Dictionary<string, List<TextBlock>> textBlocks;
        private Dictionary<string, Dictionary<string, List<ExampleEntry>>> examples;

        public ExampleCatalog(string testCaseRoot)
        {
            textBlocks = new();
            examples = new();

            foreach (string suitePath in Directory.GetDirectories(testCaseRoot))
            {
                string suiteName = Path.GetFileName(suitePath);
                textBlocks[suiteName] = new();
                examples[suiteName] = new();
                examples[suiteName][string.Empty] = new();

                foreach (string testCasePath in Directory.GetFiles(suitePath, @"*.yaml"))
                {
                    TextBlock textBlock = new TextBlock(testCasePath);
                    textBlocks[suiteName].Add(textBlock);

                    List<ExampleEntry> activeExamples = new();
                    foreach (string textLine in textBlock.TextLines)
                    {
                        Match? match = Parse.Match(textLine);
                        string indentString = match.Groups[1].Captures[0].Value;
                        string arrayIndicator = match.Groups[2].Captures[0].Value;
                        string? key = match.Groups[3].Captures.Count == 0 ? null : match.Groups[3].Captures[0].Value;
                        string value = match.Groups[4].Captures[0].Value;

                        int indentation = indentString.Length;
                        int totalIndent = indentation + arrayIndicator.Length;
                        bool isArray = arrayIndicator.Length != 0;

                        foreach (ExampleEntry activeExample in activeExamples)
                        {
                            activeExample.ConsiderNextLine(textLine, value, indentation, totalIndent, isArray);
                        }

                        if (key != null)
                        {
                            activeExamples.Add(new ExampleEntry(textLine, key, value, indentation, totalIndent, isArray));
                        }
                    }

                    foreach (ExampleEntry activeExample in activeExamples)
                    {
                        if (!examples[suiteName].TryGetValue(activeExample.Key, out List<ExampleEntry>? exampleEntries))
                        {
                            exampleEntries = new List<ExampleEntry>();
                            examples[suiteName][activeExample.Key] = exampleEntries;
                        }

                        exampleEntries.Add(activeExample);
                    }

                    examples[suiteName][string.Empty].Add(new ExampleEntry(textBlock.TextLines));
                }
            }
        }

        public IEnumerable<ExampleEntry> GetExamples(string suiteName, string key)
        {
            if (suiteName != string.Empty)
            {
                return SafeGet(examples[suiteName], key);
            }
            else
            {
                return examples.Values.Aggregate((IEnumerable<ExampleEntry>)new List<ExampleEntry>(), (agg, next) => agg.Concat(SafeGet(next, key)));
            }
        }

        private static List<ExampleEntry> SafeGet(Dictionary<string, List<ExampleEntry>> examples, string key)
        {
            return examples.TryGetValue(key, out List<ExampleEntry>? exampleEntries) ? exampleEntries : new List<ExampleEntry>();
        }
    }
}
