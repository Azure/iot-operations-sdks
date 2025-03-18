namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.IO;
    using System.Xml;

    public class CodeBlockGenerator : IDocumentationGenerator
    {
        private const string langAttr = "language";
        private const string sourceAttr = "source";

        private string language;
        private string code;

        public CodeBlockGenerator(XmlElement codeBlockElt)
        {
            language = codeBlockElt.GetAttribute(langAttr);
            code = codeBlockElt.HasAttribute(sourceAttr) ? File.ReadAllText(codeBlockElt.GetAttribute(sourceAttr)) : codeBlockElt.InnerText.Trim();
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.FencedCodeBlock(language, code);
        }
    }
}
