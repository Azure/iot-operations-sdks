namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    public class ThingGenerator
    {
        private readonly IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict;
        private readonly Dtmi interfaceId;
        private readonly int mqttVersion;

        public ThingGenerator(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, Dtmi interfaceId, int mqttVersion)
        {
            this.modelDict = modelDict;
            this.interfaceId = interfaceId;
            this.mqttVersion = mqttVersion;
        }

        public bool GenerateThing(DirectoryInfo outDir)
        {
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[interfaceId];

            ITemplateTransform interfaceThingTransform = new InterfaceThing(modelDict, interfaceId, this.mqttVersion);

            string interfaceThingText;
            try
            {
                interfaceThingText = interfaceThingTransform.TransformText();
            }
            catch (RecursionException rex)
            {
                Console.WriteLine($"Unable to generate Thing Description {interfaceThingTransform.FileName} because {rex.SchemaName.AsDtmi} has a self-referential definition");
                return false;
            }

            if (!outDir.Exists)
            {
                outDir.Create();
            }

            string filePath = Path.Combine(outDir.FullName, interfaceThingTransform.FileName);
            File.WriteAllText(filePath, interfaceThingText);

            Console.WriteLine($"  generated {filePath}");

            return true;
        }
    }
}
