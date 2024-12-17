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
        public static void GenerateEnvoys(string language, string projectName, string annexFileName, DirectoryInfo workingDir, DirectoryInfo outDir, string genNamespace, string? sdkPath, bool syncApi, bool generateClient, bool generateServer, HashSet<string> sourceFilePaths, HashSet<SchemaKind> distinctSchemaKinds)
        {
            string? relativeSdkPath = sdkPath == null || sdkPath.StartsWith("http://") || sdkPath.StartsWith("https://") ? sdkPath : Path.GetRelativePath(outDir.FullName, sdkPath);
            using (JsonDocument annexDoc = JsonDocument.Parse(File.OpenText(Path.Combine(workingDir.FullName, genNamespace, annexFileName)).ReadToEnd()))
            {
                foreach (ITemplateTransform templateTransform in EnvoyTransformFactory.GetTransforms(language, projectName, annexDoc, workingDir.FullName, relativeSdkPath, syncApi, generateClient, generateServer, sourceFilePaths, distinctSchemaKinds))
                {
                    string envoyFilePath = Path.Combine(outDir.FullName, templateTransform.FolderPath, templateTransform.FileName);
                    if (templateTransform is IUpdatingTransform updatingTransform)
                    {
                        string[] extantFiles = Directory.GetFiles(Path.Combine(outDir.FullName, templateTransform.FolderPath), updatingTransform.FilePattern);

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

            if (language == "rust")
            {
                try
                {
                    Console.WriteLine($"cargo fmt {outDir.FullName}");
                    Process.Start("cargo", $"fmt --manifest-path {Path.Combine(outDir.FullName, "Cargo.toml")}");
                }
                catch (Win32Exception)
                {
                    Console.WriteLine("cargo tool not found; install per instructions: https://doc.rust-lang.org/cargo/getting-started/installation.html");
                }
            }
        }
    }
}
