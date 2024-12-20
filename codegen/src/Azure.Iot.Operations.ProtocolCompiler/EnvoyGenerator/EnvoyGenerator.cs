namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    internal class EnvoyGenerator
    {
        public static void GenerateEnvoys(string language, string projectName, string annexFileName, DirectoryInfo workingDir, string genRoot, string genNamespace, string? sdkPath, bool syncApi, bool generateClient, bool generateServer, HashSet<string> sourceFilePaths, HashSet<SchemaKind> distinctSchemaKinds, bool genOrUpdateProj)
        {
            string? relativeSdkPath = sdkPath == null || sdkPath.StartsWith("http://") || sdkPath.StartsWith("https://") ? sdkPath : Path.GetRelativePath(genRoot, sdkPath);
            using (JsonDocument annexDoc = JsonDocument.Parse(File.OpenText(Path.Combine(workingDir.FullName, genNamespace, annexFileName)).ReadToEnd()))
            {
                foreach (ITemplateTransform templateTransform in EnvoyTransformFactory.GetTransforms(language, projectName, annexDoc, workingDir.FullName, relativeSdkPath, syncApi, generateClient, generateServer, sourceFilePaths, distinctSchemaKinds, genRoot, genOrUpdateProj))
                {
                    string envoyFilePath = Path.Combine(genRoot, templateTransform.FolderPath, templateTransform.FileName);
                    if (templateTransform is IUpdatingTransform updatingTransform)
                    {
                        string[] extantFiles = Directory.GetFiles(Path.Combine(genRoot, templateTransform.FolderPath), updatingTransform.FilePattern);

                        if (extantFiles.Any())
                        {
                            if (updatingTransform.TryUpdateFile(extantFiles.First()))
                            {
                                Console.WriteLine($"  updated {extantFiles.First()}");
                            }

                            continue;
                        }
                    }

                    string envoyDirPath = Path.GetDirectoryName(envoyFilePath)!;
                    if (!Directory.Exists(envoyDirPath))
                    {
                        Directory.CreateDirectory(envoyDirPath);
                    }

                    File.WriteAllText(envoyFilePath, templateTransform.TransformText());
                    Console.WriteLine($"  generated {envoyFilePath}");
                    sourceFilePaths.Add(envoyFilePath);
                }
            }

            if (language == "rust" && genOrUpdateProj)
            {
                try
                {
                    string cargoDir = Directory.GetParent(genRoot)!.FullName;
                    Console.WriteLine($"cargo fmt {cargoDir}");
                    Process.Start("cargo", $"fmt --manifest-path {Path.Combine(cargoDir, "Cargo.toml")}");
                }
                catch (Win32Exception)
                {
                    Console.WriteLine("cargo tool not found; install per instructions: https://doc.rust-lang.org/cargo/getting-started/installation.html");
                }
            }
        }
    }
}
