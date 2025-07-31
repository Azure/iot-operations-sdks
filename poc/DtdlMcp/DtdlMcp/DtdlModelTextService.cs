namespace DtdlMcp
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using Azure.Iot.Operations.ProtocolCompilerLib;
    using ModelContextProtocol.Protocol;

    public class DtdlModelTextService
    {
        const string ModelRepoIndexFilePath = "C:\\Users\\johndo\\Git\\iot-operations-sdks\\poc\\Opc2Dtdl\\ModelIndex.json";

        private ReaderWriterLock rwLock;
        private readonly Dictionary<string, Dictionary<string, string>> codeFilesForModelId;
        private readonly Resolver resolver;

        public DtdlModelTextService()
        {
            rwLock = new ReaderWriterLock();
            codeFilesForModelId = new();
            resolver = new Resolver();
        }

        public List<SpecInfo> GetModelIndex()
        {
            return JsonSerializer.Deserialize<List<SpecInfo>>(File.ReadAllText(ModelRepoIndexFilePath)) ?? [];
        }

        public CallToolResult GetGeneratedClassFileNames(string modelId, string projectName, bool generateClient, bool generateServer, bool defaultImpl, bool generateProject)
        {
            Dictionary<string, string> codeFiles;

            try
            {
                codeFiles = CompositeGenerator.GetCodeFilesForModelId(modelId, projectName, "csharp", resolver.ResolveAsync, null, generateClient, generateServer, defaultImpl, generateProject).Result;
                rwLock.AcquireWriterLock(Timeout.Infinite);
                codeFilesForModelId[modelId] = codeFiles;
                rwLock.ReleaseWriterLock();
            }
            catch (Exception ex)
            {
                rwLock.ReleaseWriterLock();
                return new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = ex.Message } }, };
            }

            return new CallToolResult
            {
                IsError = false,
                Content = new List<ContentBlock> { new TextContentBlock { Text = "[" + string.Join(", ", codeFiles.Keys.Select(k => $"\"{k}\"")) + "]" } },
            };
        }

        public CallToolResult GetGeneratedClass(string modelId, string classFileName)
        {
            rwLock.AcquireReaderLock(Timeout.Infinite);
            if (!codeFilesForModelId.TryGetValue(modelId, out Dictionary<string, string>? codeFiles))
            {
                rwLock.ReleaseReaderLock();
                return new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = $"class file name '{classFileName}' not found." } } };
            }

            if (!codeFiles.TryGetValue(classFileName, out string? codeFile))
            {
                return new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = $"class file name '{classFileName}' not found." } } };
            }

            return new CallToolResult
            {
                IsError = false,
                Content = new List<ContentBlock> { new TextContentBlock { Text = codeFile } },
            };
        }
    }
}
