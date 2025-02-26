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
        public static void GenerateEnvoys(string language, string projectName, string annexFileName, DirectoryInfo outDir, DirectoryInfo workingDir, string genRoot, CodeName genNamespace, string? sdkPath, bool syncApi, bool generateClient, bool generateServer, bool defaultImpl, bool generateProject)
        {
            string? relativeSdkPath = sdkPath == null || sdkPath.StartsWith("http://") || sdkPath.StartsWith("https://") ? sdkPath : Path.GetRelativePath(outDir.FullName, sdkPath);
            using (JsonDocument annexDoc = JsonDocument.Parse(File.OpenText(Path.Combine(workingDir.FullName, genNamespace.GetFolderName(TargetLanguage.Independent), annexFileName)).ReadToEnd()))
            {
                foreach (ITemplateTransform templateTransform in EnvoyTransformFactory.GetTransforms(language, projectName, annexDoc, workingDir.FullName, relativeSdkPath, syncApi, generateClient, generateServer, defaultImpl, genRoot, generateProject))
                {
                    string envoyFilePath = Path.Combine(genRoot, templateTransform.FolderPath, templateTransform.FileName);
                    if (templateTransform is IUpdatingTransform updatingTransform)
                    {
                        if (!generateProject)
                        {
                            continue;
                        }

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
                }
            }

            if (language == "rust" && generateProject)
            {
                try
                {
                    RunCargo($"fmt --manifest-path {Path.Combine(outDir.FullName, "Cargo.toml")}", display: true);
                    RunCargo("install --locked cargo-machete@0.7.0", display: false);
                    RunCargo($"machete --fix {outDir.FullName}", display: true);
                }
                catch (Win32Exception)
                {
                    Console.WriteLine("cargo tool not found; install per instructions: https://doc.rust-lang.org/cargo/getting-started/installation.html");
                }
            }
        }

        private static void RunCargo(string args, bool display)
        {
            if (display)
            {
                Console.WriteLine($"cargo {args}");
            }

            using (Process cargo = new Process())
            {
                cargo.StartInfo.FileName = "cargo";
                cargo.StartInfo.Arguments = args;
                cargo.StartInfo.UseShellExecute = false;
                cargo.StartInfo.RedirectStandardOutput = true;
                cargo.Start();
                cargo.WaitForExit();
            }
        }
    }
}
