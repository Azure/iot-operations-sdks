namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class ExampleGenerator : IDocumentationGenerator
    {
        private const string keyAttr = "key";
        private const string valueAttr = "value";
        private const string minChildrenAttr = "minChildren";
        private const string maxWidthAttr = "maxWidth";
        private const string minLinesAttr = "minLines";
        private const string maxLinesAttr = "maxLines";
        private const string targetLinesAttr = "targetLines";

        private static int? defaultMinChildren = null;
        private static int? defaultMaxWidth = null;
        private static int? defaultMinLines = null;
        private static int? defaultMaxLines = null;
        private static int defaultTargetLines = 10;

        private ExampleCatalog exampleCatalog;
        private string suiteName;
        private string key;
        private string? value;
        private int? minChildren;
        private int? maxWidth;
        private int? minLines;
        private int? maxLines;
        private int targetLines;

        private List<string> includeKeys;
        private List<string> excludeKeys;

        public static void ProcessHeader(XmlElement headerElt)
        {
            for (int i = 0; i < headerElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = headerElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "ExampleDefaults")
                {
                    XmlElement defaultsElt = (XmlElement)xmlNode;

                    if (defaultsElt.HasAttribute(minChildrenAttr))
                    {
                        defaultMinChildren = int.Parse(defaultsElt.GetAttribute(minChildrenAttr));
                    }

                    if (defaultsElt.HasAttribute(maxWidthAttr))
                    {
                        defaultMaxWidth = int.Parse(defaultsElt.GetAttribute(maxWidthAttr));
                    }

                    if (defaultsElt.HasAttribute(minLinesAttr))
                    {
                        defaultMinLines = int.Parse(defaultsElt.GetAttribute(minLinesAttr));
                    }

                    if (defaultsElt.HasAttribute(maxLinesAttr))
                    {
                        defaultMaxLines = int.Parse(defaultsElt.GetAttribute(maxLinesAttr));
                    }

                    if (defaultsElt.HasAttribute(targetLinesAttr))
                    {
                        defaultTargetLines = int.Parse(defaultsElt.GetAttribute(targetLinesAttr));
                    }
                }
            }
        }

        public ExampleGenerator(XmlElement exampleElt, ExampleCatalog exampleCatalog, string suiteName)
        {
            this.exampleCatalog = exampleCatalog;
            this.suiteName = exampleElt.HasAttribute("suite") ? exampleElt.GetAttribute("suite") : suiteName;

            this.key = exampleElt.GetAttribute(keyAttr);
            this.value = exampleElt.HasAttribute(valueAttr) ? exampleElt.GetAttribute(valueAttr) : null;

            this.minChildren = exampleElt.HasAttribute(minChildrenAttr) ? int.Parse(exampleElt.GetAttribute(minChildrenAttr)) : defaultMinChildren;
            this.maxWidth = exampleElt.HasAttribute(maxWidthAttr) ? int.Parse(exampleElt.GetAttribute(maxWidthAttr)) : defaultMaxWidth;

            this.minLines = exampleElt.HasAttribute(minLinesAttr) ? int.Parse(exampleElt.GetAttribute(minLinesAttr)) : defaultMinLines;
            this.maxLines = exampleElt.HasAttribute(maxLinesAttr) ? int.Parse(exampleElt.GetAttribute(maxLinesAttr)) : defaultMaxLines;
            this.targetLines = exampleElt.HasAttribute(targetLinesAttr) ? int.Parse(exampleElt.GetAttribute(targetLinesAttr)) : defaultTargetLines;

            includeKeys = new List<string>();
            excludeKeys = new List<string>();

            for (int i = 0; i < exampleElt.ChildNodes.Count; i++)
            {
                XmlNode subNode = exampleElt.ChildNodes[i]!;
                if (subNode.NodeType == XmlNodeType.Element)
                {
                    XmlElement subElt = (XmlElement)subNode;
                    if (subNode.NodeType == XmlNodeType.Element && subNode.Name == "Include")
                    {
                        includeKeys.Add(subElt.GetAttribute("key"));
                    }
                    else if (subNode.NodeType == XmlNodeType.Element && subNode.Name == "Exclude")
                    {
                        excludeKeys.Add(subElt.GetAttribute("key"));
                    }
                }
            }
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            IEnumerable<ExampleEntry> matchingExamples = exampleCatalog.GetExamples(suiteName, key);

            if (value != null)
            {
                matchingExamples = matchingExamples.Where(e => e.Value == value);
            }

            if (minChildren != null)
            {
                matchingExamples = matchingExamples.Where(e => e.ChildCount >= minChildren);
            }

            if (maxWidth != null)
            {
                matchingExamples = matchingExamples.Where(e => e.MaxLineWidth <= maxWidth);
            }

            if (minLines != null)
            {
                matchingExamples = matchingExamples.Where(e => e.TextLines.Count >= minLines);
            }

            if (maxLines != null)
            {
                matchingExamples = matchingExamples.Where(e => e.TextLines.Count <= maxLines);
            }

            foreach (var includeKey in includeKeys)
            {
                matchingExamples = matchingExamples.Where(e => e.TextLines.Any(l => $" {l}".Contains($" {includeKey}:")));
            }

            foreach (var excludeKey in excludeKeys)
            {
                matchingExamples = matchingExamples.Where(e => !e.TextLines.Any(l => $" {l}".Contains($" {excludeKey}:")));
            }

            ExampleEntry? optimalExample = Enumerable.MinBy(matchingExamples, e => Math.Abs(e.TextLines.Count - targetLines));

            if (optimalExample != null)
            {
                markdownFile.FencedCodeBlock("yaml", optimalExample.ToString());
            }
            else
            {
                string? valueConstraint = value != null ? $"value of '{value}'" : null;
                string? minChildrenConstraint = minChildren != null ? $"at least {minChildren} children" : null;
                string? maxWidthConstraint = maxWidth != null ? $"no lines longer than {maxWidth} characters" : null;
                string? minLinesConstraint = minLines != null ? $"at least {minLines} lines" : null;
                string? maxLinesConstraint = maxLines != null ? $"at most {maxLines} lines" : null;
                string? includeKeysConstraint = includeKeys.Any() ? $"subkey {string.Join(" and ", includeKeys.Select(k => $"'{k}'"))}" : null;
                string? excludeKeysConstraint = excludeKeys.Any() ? $"no subkey {string.Join(" or ", excludeKeys.Select(k => $"'{k}'"))}" : null;

                var constraints = new List<string?> { valueConstraint, minChildrenConstraint, maxWidthConstraint, minLinesConstraint, maxLinesConstraint, includeKeysConstraint, excludeKeysConstraint }.Where(c => c != null);
                string constraintDesc = constraints.Any() ? $" with {string.Join(" and ", constraints)}" : string.Empty;
                string suiteDesc = suiteName != string.Empty ? $"suite {suiteName}" : "any suite";
                string errorString = $"No available example in {suiteDesc} for key '{key}'{constraintDesc}";

                Alert.Error(errorString);
                markdownFile.Blockquote($"**{errorString}**");
            }
        }
    }
}
