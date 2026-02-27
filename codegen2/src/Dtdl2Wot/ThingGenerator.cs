// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Dtdl2Wot
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using DTDLParser;
    using DTDLParser.Models;

    public class ThingGenerator
    {
        private readonly ITemplateTransform transform;
        private readonly DirectoryInfo outDir;

        public ThingGenerator(ITemplateTransform transform, DirectoryInfo outDir)
        {
            this.transform = transform;
            this.outDir = outDir;
        }

        public bool GenerateThing()
        {
            string outputText;
            try
            {
                outputText = transform.TransformText();
            }
            catch (RecursionException rex)
            {
                Console.WriteLine($"Unable to generate Thing Description {transform.FileName} because {rex.SchemaName.AsDtmi} has a self-referential definition");
                return false;
            }

            if (!outDir.Exists)
            {
                outDir.Create();
            }

            string filePath = Path.Combine(outDir.FullName, transform.FileName);
            File.WriteAllText(filePath, outputText);

            Console.WriteLine($"  generated {filePath}");

            return true;
        }
    }
}
