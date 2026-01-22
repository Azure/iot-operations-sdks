namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;

    public partial class DotNetProject : IEnvoyTemplateTransform
    {
        internal const string SdkPackageName = "Azure.Iot.Operations.Protocol";
        internal const string SdkProjectName = $"{SdkPackageName}.csproj";

        private static readonly Regex MajorMinorRegex = new("^(\\d+\\.\\d+).", RegexOptions.Compiled);

        private readonly string projectName;
        private readonly string? sdkProjPath;
        private readonly string? sdkVersion;
        private readonly string? targetFramework;

        public DotNetProject(string projectName, string? sdkPath)
        {
            this.projectName = projectName;
            this.sdkProjPath = sdkPath != null ? $"{sdkPath.Replace('/', '\\')}\\{SdkProjectName}" : null;

            Match? majorMinorMatch = MajorMinorRegex.Match(Assembly.GetExecutingAssembly().GetName().Version!.ToString());
            sdkVersion = majorMinorMatch.Success ? $"{majorMinorMatch.Groups[1].Captures[0].Value}.*-*" : null;

            Version frameworkVersion = new FrameworkName(Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName!).Version;
            this.targetFramework = $"net{frameworkVersion}";
        }

        public string FileName { get => $"{this.projectName}.csproj"; }

        public string FolderPath { get => string.Empty; }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Shared; }
    }
}
