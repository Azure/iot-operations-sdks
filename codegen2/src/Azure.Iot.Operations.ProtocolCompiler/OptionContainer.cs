﻿namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.IO;

    /// <summary>
    /// Custom container for holding CLI options.
    /// </summary>
    public class OptionContainer
    {
        /// <summary>Gets or sets the file(s) containing WoT Thing Description(s) to process.</summary>
        public required FileInfo[] ThingFiles { get; set; }

        /// <summary>Gets or sets the filespec(s) of files containing external schema definitions.</summary>
        public required string[] ExtSchemaFiles { get; set; }

        /// <summary>Gets or sets the directory for storing temporary files.</summary>
        public required DirectoryInfo WorkingDir { get; set; }

        /// <summary>Gets or sets the directory for receiving generated code.</summary>
        public required DirectoryInfo OutputDir { get; set; }

        /// <summary>Gets or sets a subdirectory under OutputDir for generated source code.</summary>
        public required string OutputSourceSubdir { get; set; }

        /// <summary>Gets or sets a namespace for generated code.</summary>
        public required string GenNamespace { get; set; }

        /// <summary>Gets or sets a local path or feed URL for Azure.Iot.Operations.Protocol SDK.</summary>
        public string? SdkPath { get; set; }

        /// <summary>Gets or sets the programming language for generated code.</summary>
        public required string Language { get; set; }

        /// <summary>Gets or sets an indication of whether to generate only client-side code.</summary>
        public bool ClientOnly { get; set; }

        /// <summary>Gets or sets an indication of whether to generate only server-side code.</summary>
        public bool ServerOnly { get; set; }

        /// <summary>Gets or sets an indication of whether to suppress generation of a project.</summary>
        public bool NoProj { get; set; }

        /// <summary>Gets or sets an indication of whether to substitute virtual methods for abstract methods.</summary>
        public bool DefaultImpl { get; set; }
    }
}
