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
        /// <summary>Gets or sets one or more glob patterns used to locate OPC UA Nodeset files to process.</summary>
        public required string[] NodeSetsSpec { get; set; }

        /// <summary>Gets or sets the path to a folder for placing files that will each contain a collection of WoT Things.</summary>
        public required DirectoryInfo OutputDir { get; set; }

        /// <summary>Gets or sets an indication of whether to integrate all referenced Thing Models into each Thing collection.</summary>
        public bool Integrate { get; set; }

        /// <summary>Gets or sets an indication of whether to add a 'dov:includeInherited' property to root-level forms.</summary>
        public bool InheritVars { get; set; }

        /// <summary>Gets or sets an indication of whether to include Thing Descriptions in the Thing collection.</summary>
        public bool IncludeTDs { get; set; }
    }
}
