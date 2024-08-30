namespace Akri.Dtdl.Codegen.UnitTests.EnvoyGeneratorTests
{
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Xml;
    using NuGet.Versioning;
    using Akri.Dtdl.Codegen;

    public class DotNetProjectTests
    {
        private const string rootPath = "../../../EnvoyGeneratorTests";
        private const string csprojPath = $"{rootPath}/csproj";

        private const string currentSdkPath = "new-path";

        private string currentSdkVersion;

        public DotNetProjectTests()
        {
            Regex MajorMinorRegex = new("^(\\d+\\.\\d+).", RegexOptions.Compiled);
            Match? majorMinorMatch = MajorMinorRegex.Match(ThisAssembly.AssemblyVersion);

            Assert.True(majorMinorMatch.Success);

            currentSdkVersion = $"{majorMinorMatch.Groups[1].Captures[0].Value}.*-*";
        }

        [Theory]
        [InlineData(PayloadFormat.Avro, "NoDependentPackageRefs", true, false)]
        [InlineData(PayloadFormat.Cbor, "NoDependentPackageRefs", true, false)]
        [InlineData(PayloadFormat.Proto2, "NoDependentPackageRefs", true, false)]
        [InlineData(PayloadFormat.Proto3, "NoDependentPackageRefs", true, false)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefNoVersion", true, false)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefNoVersion", true, false)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefNoVersion", true, false)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefNoVersion", true, false)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefOldMajorVersion", true, false)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefOldMajorVersion", true, false)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefOldMajorVersion", true, false)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefOldMajorVersion", true, false)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefOldMinorVersion", true, false)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefOldMinorVersion", true, false)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefOldMinorVersion", true, false)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefOldMinorVersion", true, false)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefOldPatchVersion", true, false)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefOldPatchVersion", true, false)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefOldPatchVersion", true, false)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefOldPatchVersion", true, false)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefCurrentVersion", false, false)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefCurrentVersion", false, false)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefCurrentVersion", false, false)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefCurrentVersion", false, false)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefFutureMajorVersion", false, true)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefFutureMajorVersion", false, true)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefFutureMajorVersion", false, true)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefFutureMajorVersion", false, true)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefFutureMinorVersion", false, true)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefFutureMinorVersion", false, true)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefFutureMinorVersion", false, true)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefFutureMinorVersion", false, true)]
        [InlineData(PayloadFormat.Avro, "AvroPackageRefFuturePatchVersion", false, true)]
        [InlineData(PayloadFormat.Cbor, "CborPackageRefFuturePatchVersion", false, true)]
        [InlineData(PayloadFormat.Proto2, "ProtoPackageRefFuturePatchVersion", false, true)]
        [InlineData(PayloadFormat.Proto3, "ProtoPackageRefFuturePatchVersion", false, true)]
        public void TestUpdatePackageRefs(string genFormat, string csprojName, bool updateNeeded, bool expectFutureVersion)
        {
            var xmlDoc = new XmlDocument();
            using (var fileStream = new FileStream($"{csprojPath}/{csprojName}.xml", FileMode.Open, FileAccess.Read))
            {
                xmlDoc.Load(fileStream);
            }

            var dotNetProject = new DotNetProject(string.Empty, genFormat, ".");
            bool updated = dotNetProject.TryUpdateXmlDoc(xmlDoc);
            Assert.Equal(updateNeeded, updated);

            foreach (var packageVersion in DotNetProject.serializerPackageVersions[genFormat])
            {
                XmlElement? refElt = (XmlElement?)xmlDoc.DocumentElement!.SelectSingleNode($"/Project/ItemGroup/PackageReference[@Include='{packageVersion.Item1}']");
                Assert.NotNull(refElt);
                Assert.True(refElt.HasAttribute("Version"));

                if (expectFutureVersion)
                {
                    Assert.True(SemanticVersion.Parse(packageVersion.Item2) < SemanticVersion.Parse(refElt.GetAttribute("Version")));
                }
                else
                {
                    Assert.Equal(packageVersion.Item2, refElt.GetAttribute("Version"));
                }
            }
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void TestUpdateSdkPackageRefNewVersion(bool usePackage, bool updateNeeded)
        {
            string xmlText = File.ReadAllText($"{csprojPath}/SdkPackageRefTemplateVersion.xml");
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlText.Replace("[VERSION]", currentSdkVersion));
            TestUpdateSdkRefInDoc(xmlDoc, usePackage, updateNeeded);
        }

        [Theory]
        [InlineData("NoSdkRef", true, true)]
        [InlineData("NoSdkRef", false, true)]
        [InlineData("SdkPackageRefNoVersion", true, true)]
        [InlineData("SdkPackageRefNoVersion", false, true)]
        [InlineData("SdkPackageRefOldVersion", true, true)]
        [InlineData("SdkPackageRefOldVersion", false, true)]
        [InlineData("SdkProjectRefOldPath", true, true)]
        [InlineData("SdkProjectRefOldPath", false, true)]
        [InlineData("SdkProjectRefNewPath", true, true)]
        [InlineData("SdkProjectRefNewPath", false, false)]
        public void TestUpdateSdkRef(string csprojName, bool usePackage, bool updateNeeded)
        {
            var xmlDoc = new XmlDocument();
            using (var fileStream = new FileStream($"{csprojPath}/{csprojName}.xml", FileMode.Open, FileAccess.Read))
            {
                xmlDoc.Load(fileStream);
            }

            TestUpdateSdkRefInDoc(xmlDoc, usePackage, updateNeeded);
        }

        private void TestUpdateSdkRefInDoc(XmlDocument xmlDoc, bool usePackage, bool updateNeeded)
        {
            var dotNetProject = new DotNetProject(string.Empty, PayloadFormat.Raw, usePackage ? null : currentSdkPath);
            bool updated = dotNetProject.TryUpdateXmlDoc(xmlDoc);
            Assert.Equal(updateNeeded, updated);

            XmlElement? projectRefElt = (XmlElement?)xmlDoc.DocumentElement!.SelectSingleNode($"/Project/ItemGroup/ProjectReference[contains(@Include, '{DotNetProject.SdkProjectName}')]");
            XmlElement? packageRefElt = (XmlElement?)xmlDoc.DocumentElement!.SelectSingleNode($"/Project/ItemGroup/PackageReference[@Include='{DotNetProject.SdkPackageName}']");

            if (usePackage)
            {
                Assert.Null(projectRefElt);
                Assert.NotNull(packageRefElt);

                Assert.True(packageRefElt.HasAttribute("Version"));
                Assert.Equal(currentSdkVersion, packageRefElt.GetAttribute("Version"));
            }
            else
            {
                Assert.NotNull(projectRefElt);
                Assert.Null(packageRefElt);

                Assert.Equal($"{currentSdkPath}\\{DotNetProject.SdkProjectName}", projectRefElt.GetAttribute("Include"));
            }
        }
    }
}
