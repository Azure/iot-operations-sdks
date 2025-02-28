namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    public class MarkdownFile
    {
        private const int CharactersPerIndentLevel = 2;

        private readonly StreamWriter streamWriter;

        private bool suppressBreak;

        public MarkdownFile(string filePath)
        {
            this.streamWriter = new StreamWriter(filePath, false, Encoding.UTF8);
            this.suppressBreak = true;
        }

        public static string ToReference(string text)
        {
            return Regex.Replace(text.ToLowerInvariant(), @"[^\w ]", string.Empty).Replace(' ', '-');
        }

        public void Comment(string text)
        {
            this.streamWriter.WriteLine($"[//]: # ({text})");
            this.streamWriter.WriteLine();
        }

        public void Title(string title)
        {
            this.streamWriter.WriteLine($"# {title}");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void Heading(string heading)
        {
            this.streamWriter.WriteLine($"## {heading}");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void Subheading(string subheading)
        {
            this.streamWriter.WriteLine($"### {subheading}");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void Subsubheading(string subsubheading)
        {
            this.streamWriter.WriteLine($"#### {subsubheading}");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void Text(string text)
        {
            this.streamWriter.WriteLine(text);
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void Bullet(string text, int indentationLevel = 0)
        {
            this.streamWriter.WriteLine($"{string.Empty.PadLeft(CharactersPerIndentLevel * indentationLevel, ' ')}* {text}");
            this.suppressBreak = false;
        }

        public void Break()
        {
            if (!this.suppressBreak)
            {
                this.streamWriter.WriteLine();
                this.suppressBreak = true;
            }
        }

        public void Blockquote(string text)
        {
            this.streamWriter.WriteLine($"> {text}");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void FencedCodeBlock(string language, string code)
        {
            this.streamWriter.WriteLine($"```{language}");
            this.streamWriter.Write(code);
            if (!code.EndsWith('\n'))
            {
                this.streamWriter.WriteLine();
            }

            this.streamWriter.WriteLine("```");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void BeginTableRow(bool inQuote = false)
        {
            this.streamWriter.Write(inQuote ? "> |" : "|");
        }

        public void EndTableRow()
        {
            this.streamWriter.WriteLine();
            this.suppressBreak = false;
        }

        public void EndTable()
        {
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void BeginBlock()
        {
            this.streamWriter.WriteLine("```");
        }

        public void EndBlock()
        {
            this.streamWriter.WriteLine("```");
            this.streamWriter.WriteLine();
            this.suppressBreak = true;
        }

        public void Line(string text)
        {
            this.streamWriter.WriteLine(text);
        }

        public void TableSeparator()
        {
            this.streamWriter.Write($" --- |");
        }

        public void TableCell(string cellText)
        {
            this.streamWriter.Write($" {cellText} |");
        }

        public void Close()
        {
            this.streamWriter.Close();
        }
    }
}
