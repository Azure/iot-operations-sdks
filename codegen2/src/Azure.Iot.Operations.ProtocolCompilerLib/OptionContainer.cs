// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.IO;

    /// <summary>
    /// Custom container for holding CLI options.
    /// </summary>
    public class OptionContainer
    {
        /// <summary>Gets or sets the file(s) containing WoT Thing Description(s) to process for client-side and server-side generation.</summary>
        public required FileInfo[] ThingFiles { get; set; }

        /// <summary>Gets or sets the file(s) containing WoT Thing Description(s) to process for client-side generation.</summary>
        public required FileInfo[] ClientThingFiles { get; set; }

        /// <summary>Gets or sets the file(s) containing WoT Thing Description(s) to process for server-side generation.</summary>
        public required FileInfo[] ServerThingFiles { get; set; }

        /// <summary>Gets or sets the filespec(s) of files containing schema definitions.</summary>
        public required string[] SchemaFiles { get; set; }

        /// <summary>Gets or sets the file containing JSON config for deriving type names from JSON Schema names.</summary>
        public required FileInfo? TypeNamerFile { get; set; }

        /// <summary>Gets or sets the directory for storing temporary files.</summary>
        public required DirectoryInfo WorkingDir { get; set; }

        /// <summary>Gets or sets the directory for receiving generated code.</summary>
        public required DirectoryInfo OutputDir { get; set; }

        /// <summary>Gets or sets a namespace for generated code.</summary>
        public required string? GenNamespace { get; set; }

        /// <summary>Gets or sets a namespace for common code.</summary>
        public required string? CommonNamespace { get; set; }

        /// <summary>Gets or sets a local path or feed URL for Azure.Iot.Operations.Protocol SDK.</summary>
        public string? SdkPath { get; set; }

        /// <summary>Gets or sets the programming language for generated code.</summary>
        public required string Language { get; set; }

        /// <summary>Gets or sets an indifation of whether to apply Thing Model prefixes to schema type names.</summary>
        public bool PrefixSchemas { get; set; }

        /// <summary>Gets or sets an indication of whether to suppress generation of a project.</summary>
        public bool NoProj { get; set; }

        /// <summary>Gets or sets an indication of whether to substitute virtual methods for abstract methods.</summary>
        public bool DefaultImpl { get; set; }
    }
}
