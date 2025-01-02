namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Reflection;
    using System.Text.RegularExpressions;

    internal class ResourceTransform : ITemplateTransform
    {
        private const string SourceComment = "This file will be copied into the folder for generated code.";

        private readonly string destComment = $"Code generated by Azure.Iot.Operations.ProtocolCompiler v{Assembly.GetExecutingAssembly().GetName().Version}; DO NOT EDIT.";

        private readonly string resourceText;

        private static readonly Dictionary<string, LanguageDirective> languageDirectives = new()
        {
            { "csharp", new LanguageDirective("", @"Azure\.Iot\.Operations\.Protocol\.UnitTests\.(?:Serializers\.\w+|Support)") },
            { "rust", new LanguageDirective(SubPaths.Rust, @"resources::{0}") },
        };

        public ResourceTransform(string language, string projectName, string subFolder, string serializationPath, string serializationFile, string extension, string serializerCode)
        {
            LanguageDirective languageDirective = languageDirectives[language];

            this.FolderPath = Path.Combine(languageDirective.SubPath, serializationPath);

            this.FileName = $"{serializationFile}.{extension}";

            this.resourceText =
                (languageDirective.NamespaceReplacementRegex == null ?
                    serializerCode :
                    new Regex(languageDirective.NamespaceReplacementRegex).Replace(serializerCode, projectName))
                .Replace(SourceComment, destComment);
        }

        public string FileName { get; }

        public string FolderPath { get; }

        public string TransformText()
        {
            return this.resourceText;
        }

        private readonly struct LanguageDirective
        {
            public LanguageDirective(string subPath, string? namespaceReplacementRegex)
            {
                SubPath = subPath;
                NamespaceReplacementRegex = namespaceReplacementRegex;
            }

            public readonly string SubPath;
            public readonly string? NamespaceReplacementRegex;
        }
    }
}
