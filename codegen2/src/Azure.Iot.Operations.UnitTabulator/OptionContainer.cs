// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System.IO;

    /// <summary>
    /// Custom container for holding CLI options.
    /// </summary>
    public class OptionContainer
    {
        /// <summary>Gets or sets the path to a folder for placing generated files.</summary>
        public required DirectoryInfo OutputDir { get; set; }

        /// <summary>Gets or sets the programming language for generated code.</summary>
        public required string Language { get; set; }

        /// <summary>Gets or sets the kind of tables to generate.</summary>
        public required TableKind TableKind { get; set; }
    }
}
