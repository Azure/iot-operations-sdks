using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;

namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public partial class DotNetClientShim : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string serviceName;
        private readonly string testName;
        private readonly string projectComponentName;
        private readonly List<(string?, SchemaTypeInfo)> telemNameSchemas;
        private readonly List<(string, SchemaTypeInfo?, SchemaTypeInfo?)> cmdNameReqResps;
        private readonly IDotnetTranscoder transcoder;
        private readonly List<string> additionalUsings;
        private readonly bool doesCommandTargetExecutor;

        public DotNetClientShim(string genNamespace, string serviceName, string testName, string projectComponentName, List<(string?, SchemaTypeInfo)> telemNameSchemas, List<(string, SchemaTypeInfo?, SchemaTypeInfo?)> cmdNameReqResps, IDotnetTranscoder transcoder, List<string> additionalUsings, bool doesCommandTargetExecutor)
        {
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.testName = testName;
            this.projectComponentName = projectComponentName;
            this.telemNameSchemas = telemNameSchemas;
            this.cmdNameReqResps = cmdNameReqResps;
            this.transcoder = transcoder;
            this.additionalUsings = additionalUsings;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
        }

        public string FileName { get => $"{this.serviceName}ClientShim.g.cs"; }

        public string FolderPath { get => $"dotnet\\library\\{this.genNamespace}"; }
    }
}
