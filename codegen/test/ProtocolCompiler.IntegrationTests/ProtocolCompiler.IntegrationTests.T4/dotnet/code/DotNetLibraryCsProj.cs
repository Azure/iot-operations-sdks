
namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public partial class DotNetLibraryCsProj : ITemplateTransform
    {
        private readonly string testName;
        private readonly string projectComponentName;

        public DotNetLibraryCsProj(string testName, string projectComponentName)
        {
            this.testName = testName;
            this.projectComponentName = projectComponentName;
        }

        public string FileName { get => $"{this.testName}.Library.csproj"; }

        public string FolderPath { get => $"dotnet\\library"; }
    }
}
