
namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public partial class DotNetStandaloneTest : ITemplateTransform
    {
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string testName;
        private readonly List<string> testCaseNames;

        public DotNetStandaloneTest(string modelId, string serviceName, string testName, List<string> testCaseNames)
        {
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.testName = testName;
            this.testCaseNames = testCaseNames;
        }

        public string FileName { get => $"{this.testName}.StandaloneTest.g.cs"; }

        public string FolderPath { get => $"dotnet\\standalone"; }
    }
}
