namespace Akri.Dtdl.Codegen
{
    using System.Text.RegularExpressions;

    internal class SerializerTransform : ITemplateTransform
    {
        private readonly string serializerCode;

        private static readonly Dictionary<string, LanguageDirective> languageDirectives = new()
        {
            { "csharp", new LanguageDirective("", @"Azure\.Iot\.Operations\.Protocol\.UnitTests\.Serializers\.{0}", "", useSubnamespaceFolder: false) },
            { "rust", new LanguageDirective(SubPaths.Rust, null, "serialization", useSubnamespaceFolder: true) },
        };

        public SerializerTransform(string language, string projectName, string serializationFormat, string? serializationComponent, string extension, string serializerCode)
        {
            LanguageDirective languageDirective = languageDirectives[language];

            string subFolder = languageDirective.UseSubnamespaceFolder && serializationComponent != null ? serializationFormat : string.Empty;
            this.FolderPath = Path.Combine(languageDirective.SubPath, languageDirective.FolderName, subFolder);

            this.FileName = $"{serializationComponent ?? serializationFormat}.{extension}";

            this.serializerCode =
                languageDirective.NamespaceReplacementRegex == null ? serializerCode :
                new Regex(string.Format(languageDirective.NamespaceReplacementRegex, serializationFormat)).Replace(serializerCode, projectName);
        }

        public string FileName { get; }

        public string FolderPath { get; }

        public string TransformText()
        {
            return this.serializerCode;
        }

        private readonly struct LanguageDirective
        {
            public LanguageDirective(string subPath, string? namespaceReplacementRegex, string folderName, bool useSubnamespaceFolder)
            {
                SubPath = subPath;
                NamespaceReplacementRegex = namespaceReplacementRegex;
                FolderName = folderName;
                UseSubnamespaceFolder = useSubnamespaceFolder;
            }

            public readonly string SubPath;
            public readonly string? NamespaceReplacementRegex;
            public readonly string FolderName;
            public readonly bool UseSubnamespaceFolder;
        }
    }
}
