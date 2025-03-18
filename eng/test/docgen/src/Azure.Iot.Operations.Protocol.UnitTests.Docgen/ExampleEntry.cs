namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public record ExampleEntry
    {
        private static readonly Regex Leader = new(@"^(\s*(?:- )?)", RegexOptions.Compiled);
        private static readonly HashSet<string> YamlBlockIndicators = new() { ">", ">+", ">-", "|", "|+", "|-" };

        private int indentation;
        private int totalIndent;
        private bool isArray;
        private bool locked;
        private int? childCount;
        private int? maxLineWidth;

        public ExampleEntry(List<string> textLines)
        {
            Key = string.Empty;
            Value = string.Empty;

            indentation = -2;
            totalIndent = -2;
            isArray = false;

            locked = true;
            childCount = null;
            maxLineWidth = null;

            TextLines = textLines;
        }

        public ExampleEntry(string firstLine, string key, string value, int indentation, int totalIndent, bool isArray)
        {
            Key = key;
            Value = value;

            this.indentation = indentation;
            this.totalIndent = totalIndent;
            this.isArray = isArray;

            locked = false;
            childCount = null;
            maxLineWidth = null;

            TextLines = new List<string> { firstLine };
        }

        public string Key { get; }

        public string Value { get; }

        public List<string> TextLines { get; }

        public int ChildCount
        {
            get
            {
                if (childCount == null)
                {
                    if (TextLines.Count == 1 || YamlBlockIndicators.Contains(Value))
                    {
                        childCount = 0;
                    }
                    else
                    {
                        Match? match = Leader.Match(TextLines[1]);
                        string leader = match.Groups[1].Captures[0].Value;
                        childCount = TextLines.Count(l => l.StartsWith(leader));
                    }
                }

                return (int)childCount;
            }
        }

        public int MaxLineWidth
        {
            get
            {
                if (maxLineWidth == null)
                {
                    maxLineWidth = TextLines.Max(l => l.Length);
                }

                return (int)maxLineWidth;
            }
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new();
            foreach (string textLine in TextLines)
            {
                stringBuilder.AppendLine(textLine);
            }

            return stringBuilder.ToString();
        }

        public void ConsiderNextLine(string nextLine, string value, int indentation, int totalIndent, bool isArray)
        {
            if (locked)
            {
                return;
            }

            if (value.StartsWith('*') || value.StartsWith("<<"))
            {
                locked = true;
                return;
            }

            if ((this.isArray ? indentation : totalIndent) > this.indentation)
            {
                TextLines.Add(nextLine);
            }
            else
            {
                locked = true;
            }
        }
    }
}
