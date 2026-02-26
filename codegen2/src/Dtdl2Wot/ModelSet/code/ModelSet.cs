// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Dtdl2Wot
{
    using System.Collections.Generic;

    public partial class ModelSet : ITemplateTransform
    {
        private readonly List<ITemplateTransform> interfaceThingTransforms;

        public ModelSet(string inputFileName, List<ITemplateTransform> interfaceThingTransforms)
        {
            this.interfaceThingTransforms = interfaceThingTransforms;

            string baseFileName = inputFileName.EndsWith(".dtdl.json") ? inputFileName.Substring(0, inputFileName.Length - 10) :
                inputFileName.EndsWith(".json") ? inputFileName.Substring(0, inputFileName.Length - 5) :
                inputFileName;
            FileName = $"{baseFileName}.TM.json";
        }

        public string FileName { get; }

        public string FolderPath { get => string.Empty; }
    }
}
