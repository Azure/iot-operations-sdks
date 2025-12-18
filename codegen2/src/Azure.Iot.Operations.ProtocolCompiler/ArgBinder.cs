namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.CommandLine;
    using System.CommandLine.Binding;
    using System.IO;

    /// <summary>
    /// Custom arguemnt binder for CLI.
    /// </summary>
    public class ArgBinder : BinderBase<OptionContainer>
    {
        private readonly Option<FileInfo[]> thingFiles;
        private readonly Option<string[]> schemaFiles;
        private readonly Option<FileInfo?> typeNamerFile;
        private readonly Option<DirectoryInfo> outputDir;
        private readonly Option<string> workingDir;
        private readonly Option<string> genNamespace;
        private readonly Option<string?> sdkPath;
        private readonly Option<string> language;
        private readonly Option<bool> clientOnly;
        private readonly Option<bool> serverOnly;
        private readonly Option<bool> noProj;
        private readonly Option<bool> defaultImpl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgBinder"/> class.
        /// </summary>
        /// <param name="thingFiles">File(s) containing WoT Thing Description(s) to process.</param>
        /// <param name="schemaFiles">Filespec(s) of files containing schema definitions.</param>
        /// <param name="outputDir">Directory for receiving generated code.</param>
        /// <param name="workingDir">Directory for storing temporary files (relative to outDir unless path is rooted).</param>
        /// <param name="genNamespace">Namespace for generated code; null for default.</param>
        /// <param name="sdkPath">Local path or feed URL for Azure.Iot.Operations.Protocol SDK.</param>
        /// <param name="language">Programming language for generated code.</param>
        /// <param name="clientOnly">Generate only client-side code.</param>
        /// <param name="serverOnly">Generate only server-side code.</param>
        /// <param name="noProj">Suppress generation of a project.</param>
        /// <param name="defaultImpl">Generate default (empty) implementations of callbacks.</param>
        public ArgBinder(
            Option<FileInfo[]> thingFiles,
            Option<string[]> schemaFiles,
            Option<FileInfo?> typeNamerFile,
            Option<DirectoryInfo> outputDir,
            Option<string> workingDir,
            Option<string> genNamespace,
            Option<string?> sdkPath,
            Option<string> language,
            Option<bool> clientOnly,
            Option<bool> serverOnly,
            Option<bool> noProj,
            Option<bool> defaultImpl)
        {
            this.thingFiles = thingFiles;
            this.schemaFiles = schemaFiles;
            this.typeNamerFile = typeNamerFile;
            this.outputDir = outputDir;
            this.workingDir = workingDir;
            this.genNamespace = genNamespace;
            this.sdkPath = sdkPath;
            this.language = language;
            this.clientOnly = clientOnly;
            this.serverOnly = serverOnly;
            this.noProj = noProj;
            this.defaultImpl = defaultImpl;
        }

        /// <inheritdoc/>
        protected override OptionContainer GetBoundValue(BindingContext bindingContext)
        {
            DirectoryInfo outputDir = bindingContext.ParseResult.GetValueForOption(this.outputDir)!;
            string workingDir = bindingContext.ParseResult.GetValueForOption(this.workingDir)!;

            return new OptionContainer()
            {
                ThingFiles = bindingContext.ParseResult.GetValueForOption(this.thingFiles)!,
                SchemaFiles = bindingContext.ParseResult.GetValueForOption(this.schemaFiles)!,
                TypeNamerFile = bindingContext.ParseResult.GetValueForOption(this.typeNamerFile),
                OutputDir = outputDir,
                WorkingDir = Path.IsPathRooted(workingDir) ? new DirectoryInfo(workingDir) : new DirectoryInfo(Path.Combine(outputDir.FullName, workingDir)),
                GenNamespace = bindingContext.ParseResult.GetValueForOption(this.genNamespace)!,
                SdkPath = bindingContext.ParseResult.GetValueForOption(this.sdkPath),
                Language = bindingContext.ParseResult.GetValueForOption(this.language)!,
                ClientOnly = bindingContext.ParseResult.GetValueForOption(this.clientOnly),
                ServerOnly = bindingContext.ParseResult.GetValueForOption(this.serverOnly),
                NoProj = bindingContext.ParseResult.GetValueForOption(this.noProj),
                DefaultImpl = bindingContext.ParseResult.GetValueForOption(this.defaultImpl),
            };
        }
    }
}
