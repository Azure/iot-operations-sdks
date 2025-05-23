﻿namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.CommandLine;
    using System.CommandLine.Binding;
    using System.IO;

    /// <summary>
    /// Custom arguemnt binder for CLI.
    /// </summary>
    public class ArgBinder : BinderBase<OptionContainer>
    {
        private readonly Option<FileInfo[]> modelFile;
        private readonly Option<string?> modelId;
        private readonly Option<string?> workingDir;
        private readonly Option<DirectoryInfo> outDir;
        private readonly Option<string?> genNamespace;
#if DEBUG
        private readonly Option<string?> sharedPrefix;
        private readonly Option<string?> sdkPath;
#endif
        private readonly Option<string> lang;
        private readonly Option<bool> thingOnly;
        private readonly Option<bool> clientOnly;
        private readonly Option<bool> serverOnly;
        private readonly Option<bool> noProj;
        private readonly Option<bool> defaultImpl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgBinder"/> class.
        /// </summary>
        /// <param name="modelFile">File(s) containing DTDL model(s) to process.</param>
        /// <param name="modelId">DTMI of Interface to use for codegen (not needed when model has only one Mqtt Interface).</param>
        /// <param name="workingDir">Directory for storing temporary files (relative to outDir unless path is rooted).</param>
        /// <param name="outDir">Directory for receiving generated code.</param>
        /// <param name="genNamespace">Namespace for generated code.</param>
#if DEBUG
        /// <param name="sharedPrefix">DTMI prefix of shared schemas.</param>
        /// <param name="sdkPath">Local path or feed URL for Azure.Iot.Operations.Protocol SDK.</param>
#endif
        /// <param name="lang">Programming language for generated code.</param>
        /// <param name="thingOnly">Generate only Thing Description.</param>
        /// <param name="clientOnly">Generate only client-side code.</param>
        /// <param name="serverOnly">Generate only server-side code.</param>
        public ArgBinder(
            Option<FileInfo[]> modelFile,
            Option<string?> modelId,
            Option<string?> workingDir,
            Option<DirectoryInfo> outDir,
            Option<string?> genNamespace,
#if DEBUG
            Option<string?> sharedPrefix,
            Option<string?> sdkPath,
#endif
            Option<string> lang,
            Option<bool> thingOnly,
            Option<bool> clientOnly,
            Option<bool> serverOnly,
            Option<bool> noProj,
            Option<bool> defaultImpl)
        {
            this.modelFile = modelFile;
            this.modelId = modelId;
            this.workingDir = workingDir;
            this.outDir = outDir;
            this.genNamespace = genNamespace;
#if DEBUG
            this.sharedPrefix = sharedPrefix;
            this.sdkPath = sdkPath;
#endif
            this.lang = lang;
            this.thingOnly = thingOnly;
            this.clientOnly = clientOnly;
            this.serverOnly = serverOnly;
            this.noProj = noProj;
            this.defaultImpl = defaultImpl;
        }

        /// <inheritdoc/>
        protected override OptionContainer GetBoundValue(BindingContext bindingContext) =>
            new OptionContainer()
            {
                ModelFiles = bindingContext.ParseResult.GetValueForOption(this.modelFile)!,
                ModelId = bindingContext.ParseResult.GetValueForOption(this.modelId),
                WorkingDir = bindingContext.ParseResult.GetValueForOption(this.workingDir),
                OutDir = bindingContext.ParseResult.GetValueForOption(this.outDir)!,
                GenNamespace = bindingContext.ParseResult.GetValueForOption(this.genNamespace),
#if DEBUG
                SharedPrefix = bindingContext.ParseResult.GetValueForOption(this.sharedPrefix),
                SdkPath = bindingContext.ParseResult.GetValueForOption(this.sdkPath),
#else
                SharedPrefix = null,
                SdkPath = null,
#endif
                Lang = bindingContext.ParseResult.GetValueForOption(this.lang)!,
                ThingOnly = bindingContext.ParseResult.GetValueForOption(this.thingOnly),
                ClientOnly = bindingContext.ParseResult.GetValueForOption(this.clientOnly),
                ServerOnly = bindingContext.ParseResult.GetValueForOption(this.serverOnly),
                NoProj = bindingContext.ParseResult.GetValueForOption(this.noProj),
                DefaultImpl = bindingContext.ParseResult.GetValueForOption(this.defaultImpl),
            };
    }
}
