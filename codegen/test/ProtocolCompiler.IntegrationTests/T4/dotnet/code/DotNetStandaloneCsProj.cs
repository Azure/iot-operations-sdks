
namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public partial class DotNetStandaloneCsProj : ITemplateTransform
    {
        private readonly string testName;

        public DotNetStandaloneCsProj(string testName)
        {
            this.testName = testName;
        }

        public string FileName { get => $"{this.testName}.Standalone.csproj"; }

        public string FolderPath { get => $"dotnet\\standalone"; }
    }
}
