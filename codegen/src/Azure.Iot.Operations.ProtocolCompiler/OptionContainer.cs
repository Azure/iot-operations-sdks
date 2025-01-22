namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.IO;

    /// <summary>
    /// Custom container for holding CLI options.
    /// </summary>
    public class OptionContainer
    {
        /// <summary>Gets or sets the file(s) containing DTDL model(s) to process.</summary>
        public required FileInfo[] ModelFiles { get; set; }

        /// <summary>Gets or sets the DTMI of the Interface to use for codegen.</summary>
        public string? ModelId { get; set; }

        /// <summary>Gets or sets a directory or URL from which to retrieve referenced models.</summary>
        public string? DmrRoot { get; set; }

        /// <summary>Gets or sets the directory for storing temporary files.</summary>
        public string? WorkingDir { get; set; }

        /// <summary>Gets or sets the directory for receiving generated code.</summary>
        public required DirectoryInfo OutDir { get; set; }

        /// <summary>Gets or sets the programming language for generated code.</summary>
        public required string Lang { get; set; }

        /// <summary>Gets or sets an indication of whether to generate synchronous API.</summary>
        public bool Sync { get; set; }

        /// <summary>Gets or sets a local path or feed URL for Azure.Iot.Operations.Protocol SDK.</summary>
        public string? SdkPath { get; set; }

        /// <summary>Gets or sets an indication of whether to generate only client-side code.</summary>
        public bool ClientOnly { get; set; }

        /// <summary>Gets or sets an indication of whether to generate only server-side code.</summary>
        public bool ServerOnly { get; set; }

        /// <summary>Gets or sets an indication of whether to suppress generation of a project.</summary>
        public bool NoProj { get; set; }
    }
}
