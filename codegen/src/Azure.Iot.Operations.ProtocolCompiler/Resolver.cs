namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using DTDLParser;

    public class Resolver
    {
        private const string RegexKey = "regex";
        private const string PathKey = "path";
        private const string WildcardKey = "wild";
        private const string TokenRegexPattern = @"\{(\d+)\}";

        private static readonly Regex tokenRegex = new Regex(TokenRegexPattern, RegexOptions.Compiled);

        private readonly string configPath;
        private readonly Regex dtmiRegex;
        private readonly string pathTemplate;
        private readonly string? wildcard;

        public Resolver(string configPath)
        {
            this.configPath = configPath;

            if (!File.Exists(configPath))
            {
                throw new Exception($"Resolver config file {configPath} not found");
            }

            using (StreamReader configReader = File.OpenText(configPath))
            {
                using (JsonDocument configDoc = JsonDocument.Parse(configReader.ReadToEnd()))
                {
                    if (!configDoc.RootElement.TryGetProperty(RegexKey, out JsonElement regexElt))
                    {
                        throw new Exception($"Resolver config file {configPath} missing '{RegexKey}' property");
                    }

                    dtmiRegex = new Regex(regexElt.GetString()!);

                    if (!configDoc.RootElement.TryGetProperty(PathKey, out JsonElement pathElt))
                    {
                        throw new Exception($"Resolver config file {configPath} missing '{PathKey}' property");
                    }

                    pathTemplate = pathElt.GetString()!;

                    if (configDoc.RootElement.TryGetProperty(WildcardKey, out JsonElement wildcardElt))
                    {
                        wildcard = wildcardElt.GetString();
                    }
                }
            }
        }

        public IEnumerable<string> Resolve(IReadOnlyCollection<Dtmi> dtmis)
        {
            var refJsonTexts = new List<string>();
            HashSet<string> modelFilePaths = new ();

            foreach (Dtmi dtmi in dtmis)
            {
                Match dtmiMatch = dtmiRegex.Match(dtmi.AbsoluteUri);
                if (dtmiMatch.Success)
                {
                    string path = pathTemplate;
                    foreach (Match tokenMatch in tokenRegex.Matches(pathTemplate))
                    {
                        int groupIndex = int.Parse(tokenMatch.Groups[1].Captures[0].Value);
                        path = path.Replace($"{{{groupIndex}}}", dtmiMatch.Groups[groupIndex].Captures[0].Value);
                    }

                    string relativePath = Path.Combine(Path.GetDirectoryName(configPath)!, path);
                    string modelFolderPath = Path.GetDirectoryName(relativePath) ?? ".";
                    string modelFileName = Path.GetFileName(relativePath) ?? "*.json";
                    string modelFilePath;

                    if (wildcard != null)
                    {
                        string? parentFolderPath = Path.GetDirectoryName(modelFolderPath);
                        if (parentFolderPath != null)
                        {
                            string leafFolderName = Path.GetFileName(modelFolderPath);
                            modelFolderPath = Directory.GetDirectories(parentFolderPath, leafFolderName.Replace('_', '*')).FirstOrDefault(d => Path.GetFileName(d).Length == leafFolderName.Length) ?? modelFolderPath;
                        }

                        modelFilePath = Directory.GetFiles(modelFolderPath, modelFileName.Replace('_', '*')).FirstOrDefault(d => Path.GetFileName(d).Length == modelFileName.Length) ?? Path.Combine(modelFolderPath, modelFileName);
                    }
                    else
                    {
                        modelFilePath = Path.Combine(modelFolderPath, modelFileName);
                    }

                    if (File.Exists(modelFilePath) && !modelFilePaths.Contains(modelFilePath))
                    {
                        string jsonText = File.ReadAllText(modelFilePath);
                        refJsonTexts.Add(jsonText);
                        modelFilePaths.Add(modelFilePath);
                    }
                }
            }

            return refJsonTexts;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8425 // 'CancellationToken' is not decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed
        public async IAsyncEnumerable<string> ResolveAsync(IReadOnlyCollection<Dtmi> dtmis, CancellationToken _)
        {
            IEnumerable<string> values = Resolve(dtmis);
            if (values != null)
            {
                foreach (string value in values)
                {
                    yield return value;
                }
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8425 // 'CancellationToken' is not decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed
    }
}
