using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;

namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public partial class DotNetServiceShim : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string serviceName;
        private readonly string testName;
        private readonly string projectComponentName;
        private readonly List<(string?, SchemaTypeInfo)> telemNameSchemas;
        private readonly List<(string, SchemaTypeInfo?, SchemaTypeInfo?)> cmdNameReqResps;
        private readonly IDotnetTranscoder transcoder;
        private readonly List<string> additionalUsings;

        public DotNetServiceShim(string genNamespace, string serviceName, string testName, string projectComponentName, List<(string?, SchemaTypeInfo)> telemNameSchemas, List<(string, SchemaTypeInfo?, SchemaTypeInfo?)> cmdNameReqResps, IDotnetTranscoder transcoder, List<string> additionalUsings)
        {
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.testName = testName;
            this.projectComponentName = projectComponentName;
            this.telemNameSchemas = telemNameSchemas;
            this.cmdNameReqResps = cmdNameReqResps;
            this.transcoder = transcoder;
            this.additionalUsings = additionalUsings;
        }

        public string FileName { get => $"{this.serviceName}ServiceShim.g.cs"; }

        public string FolderPath { get => $"dotnet\\library\\{this.genNamespace}"; }
    }
}
