// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using System.IO;

    /// <summary>
    /// Custom container for holding CLI options.
    /// </summary>
    public class OptionContainer
    {
        /// <summary>Gets or sets the path to a folder containing OPC UA Nodeset2 files to process.</summary>
        public required DirectoryInfo NodeSetsDir { get; set; }

        /// <summary>Gets or sets the path to a folder for placing files that will each contain a collection of WoT Thing Models.</summary>
        public required DirectoryInfo OutputDir { get; set; }
    }
}
