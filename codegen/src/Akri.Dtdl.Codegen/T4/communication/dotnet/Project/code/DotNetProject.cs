namespace Akri.Dtdl.Codegen
{
    using System.IO;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using System.Xml;
    using NuGet.Versioning;

    public partial class DotNetProject : IUpdatingTransform
    {
        private const string ProjectTag = "Project";
        private const string ItemGroupTag = "ItemGroup";
        private const string PackageReferenceTag = "PackageReference";
        private const string ProjectReferenceTag = "ProjectReference";

        private const string IncludeAttr = "Include";
        private const string VersionAttr = "Version";

        internal const string SdkPackageName = "Azure.Iot.Operations.Protocol";
        internal const string SdkProjectName = $"{SdkPackageName}.csproj";

        internal static readonly Dictionary<string, List<(string, string)>> serializerPackageVersions = new()
        {
            { PayloadFormat.Avro, new List<(string, string)> { ("Apache.Avro", "1.11.1") } },
            { PayloadFormat.Cbor, new List<(string, string)> { ("Dahomey.Cbor", "1.20.1") } },
            { PayloadFormat.Json, new List<(string, string)> { ("Macross.Json.Extensions", "3.0.0") } },
            { PayloadFormat.Proto2, new List<(string, string)> { ("Google.Api.CommonProtos", "2.10.0"), ("Google.Protobuf", "3.23.1") } },
            { PayloadFormat.Proto3, new List<(string, string)> { ("Google.Api.CommonProtos", "2.10.0"), ("Google.Protobuf", "3.23.1") } },
            { PayloadFormat.Raw, new List<(string, string)> { } },
        };

        private static readonly Regex MajorMinorRegex = new("^(\\d+\\.\\d+).", RegexOptions.Compiled);

        private readonly string projectName;
        private readonly string? sdkProjPath;
        private readonly string? sdkVersion;
        private readonly string? targetFramework;
        private readonly List<(string, string)> packageVersions;

        public DotNetProject(string projectName, string genFormat, string? sdkPath)
        {
            this.projectName = projectName;
            this.sdkProjPath = sdkPath != null ? $"{sdkPath}\\{SdkProjectName}" : null;

            Match? majorMinorMatch = MajorMinorRegex.Match(Assembly.GetExecutingAssembly().GetName().Version!.ToString());
            sdkVersion = majorMinorMatch.Success ? $"{majorMinorMatch.Groups[1].Captures[0].Value}.*-*" : null;

            Version frameworkVersion = new FrameworkName(Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName!).Version;
            this.targetFramework = $"net{frameworkVersion}";

            packageVersions = serializerPackageVersions[genFormat];
        }

        public string FileName { get => $"{this.projectName}.csproj"; }

        public string FolderPath { get => string.Empty; }

        public string FilePattern { get => "*.csproj"; }

        public bool TryUpdateFile(string filePath)
        {
            var xmlDoc = new XmlDocument();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
            {
                xmlDoc.Load(fileStream);
            }

            if (TryUpdateXmlDoc(xmlDoc))
            {
                xmlDoc.Save(filePath);
                return true;
            }

            return false;
        }

        internal bool TryUpdateXmlDoc(XmlDocument xmlDoc)
        {
            XmlElement itemGroupElt = xmlDoc.CreateElement(ItemGroupTag);

            bool somePackageUpdated = UpdatePackageRefs(xmlDoc, itemGroupElt);
            bool sdkRefUpdated = UpdateSdkRef(xmlDoc, itemGroupElt);

            if (itemGroupElt.HasChildNodes)
            {
                xmlDoc.DocumentElement!.AppendChild(itemGroupElt);
            }

            return itemGroupElt.HasChildNodes || somePackageUpdated || sdkRefUpdated;
        }

        private bool UpdatePackageRefs(XmlDocument xmlDoc, XmlElement itemGroupElt)
        {
            bool somePackageUpdated = false;
            foreach (var packageVersion in packageVersions)
            {
                XmlElement? extantRefElt = (XmlElement?)xmlDoc.DocumentElement!.SelectSingleNode($"/{ProjectTag}/{ItemGroupTag}/{PackageReferenceTag}[@{IncludeAttr}='{packageVersion.Item1}']");
                if (extantRefElt == null)
                {
                    XmlElement newRefElt = xmlDoc.CreateElement(PackageReferenceTag);
                    newRefElt.SetAttribute(IncludeAttr, packageVersion.Item1);
                    newRefElt.SetAttribute(VersionAttr, packageVersion.Item2);
                    itemGroupElt.AppendChild(newRefElt);
                }
                else
                {
                    SemanticVersion extantVersion = SemanticVersion.Parse(extantRefElt.HasAttribute(VersionAttr) ? extantRefElt.GetAttribute(VersionAttr) : "0.0.0");
                    SemanticVersion newVersion = SemanticVersion.Parse(packageVersion.Item2);
                    if (newVersion > extantVersion)
                    {
                        extantRefElt.SetAttribute(VersionAttr, packageVersion.Item2);
                        somePackageUpdated = true;
                    }
                }
            }

            return somePackageUpdated;
        }

        private bool UpdateSdkRef(XmlDocument xmlDoc, XmlElement itemGroupElt)
        {
            bool sdkRefUpdated = false;
            bool sdkRefNeeded = true;

            XmlElement? projectRefElt = (XmlElement?)xmlDoc.DocumentElement!.SelectSingleNode($"/{ProjectTag}/{ItemGroupTag}/{ProjectReferenceTag}[contains(@{IncludeAttr}, '{SdkProjectName}')]");
            XmlElement? packageRefElt = (XmlElement?)xmlDoc.DocumentElement!.SelectSingleNode($"/{ProjectTag}/{ItemGroupTag}/{PackageReferenceTag}[@{IncludeAttr}='{SdkPackageName}']");

            if (projectRefElt != null) // found a project reference
            {
                if (sdkProjPath == null) // but need a package reference
                {
                    XmlNode itemGroupNode = projectRefElt.ParentNode!;
                    itemGroupNode.RemoveChild(projectRefElt);
                    if (!itemGroupNode.HasChildNodes)
                    {
                        itemGroupNode.ParentNode!.RemoveChild(itemGroupNode);
                    }
                }
                else // update project reference as needed
                {
                    sdkRefNeeded = false;
                    if (projectRefElt.GetAttribute(IncludeAttr) != sdkProjPath)
                    {
                        projectRefElt.SetAttribute(IncludeAttr, sdkProjPath);
                        sdkRefUpdated = true;
                    }
                }
            }

            if (packageRefElt != null) // found a package reference
            {
                if (sdkProjPath != null) // but need a project reference
                {
                    XmlNode itemGroupNode = packageRefElt.ParentNode!;
                    itemGroupNode.RemoveChild(packageRefElt);
                    if (!itemGroupNode.HasChildNodes)
                    {
                        itemGroupNode.ParentNode!.RemoveChild(itemGroupNode);
                    }
                }
                else // update package reference as needed
                {
                    sdkRefNeeded = false;
                    if (sdkVersion != null && (!packageRefElt.HasAttribute(VersionAttr) || packageRefElt.GetAttribute(VersionAttr) != sdkVersion))
                    {
                        packageRefElt.SetAttribute(VersionAttr, sdkVersion);
                        sdkRefUpdated = true;
                    }
                }
            }

            if (sdkRefNeeded)
            {
                if (sdkProjPath != null)
                {
                    XmlElement newRefElt = xmlDoc.CreateElement(ProjectReferenceTag);
                    newRefElt.SetAttribute(IncludeAttr, sdkProjPath);
                    itemGroupElt.AppendChild(newRefElt);
                }
                else
                {
                    XmlElement newRefElt = xmlDoc.CreateElement(PackageReferenceTag);
                    newRefElt.SetAttribute(IncludeAttr, SdkPackageName);
                    if (sdkVersion != null)
                    {
                        newRefElt.SetAttribute(VersionAttr, sdkVersion);
                    }

                    itemGroupElt.AppendChild(newRefElt);
                }

            }

            return sdkRefUpdated;
        }
    }
}
