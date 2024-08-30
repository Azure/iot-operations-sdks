namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    public class TextBlock
    {
        private static readonly Regex AllBlank = new(@"^\s*$", RegexOptions.Compiled);
        private static readonly Regex Anchor = new(@"^(\s*)- &", RegexOptions.Compiled);
        private static readonly Regex HeadAndTail = new(@"^(\s*)(?:  |- )(.*)$", RegexOptions.Compiled);

        public TextBlock(string filePath)
        {
            TextLines = new();

            bool listifyNextEntry = false;
            using StreamReader testCaseReader = File.OpenText(filePath);
            for (string? line = testCaseReader.ReadLine(); line != null; line = testCaseReader.ReadLine())
            {
                if (line.Length == 0 || AllBlank.IsMatch(line) || line == "---" || line == "...")
                {
                    continue;
                }

                if (Anchor.IsMatch(line))
                {
                    listifyNextEntry = true;
                    continue;
                }

                if (listifyNextEntry)
                {
                    Match? match = HeadAndTail.Match(line);
                    line = $"{match.Groups[1].Captures[0]}- {match.Groups[2].Captures[0]}";
                    listifyNextEntry = false;
                }

                TextLines.Add(line);
            }
        }

        public List<string> TextLines { get; }
    }
}
