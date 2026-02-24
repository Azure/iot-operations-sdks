// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System;
    using System.IO;
    using System.Reflection;

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine($"Usage: {Assembly.GetExecutingAssembly().GetName().Name} OUTPUT_DIR");
                return;
            }

            DirectoryInfo outDir = new DirectoryInfo(args[0]);
            if (!outDir.Exists)
            {
                outDir.Create();
            }

            UnitReader unitReader = new UnitReader();

            EceCodes eceCodes = new EceCodes(unitReader.EceCodesMap);
            string eceCodesFilePath = Path.Combine(outDir.FullName, eceCodes.FileName);
            File.WriteAllText(eceCodesFilePath, eceCodes.TransformText());

            UnitInfos unitInfos = new UnitInfos(unitReader.UnitInfosMap);
            string unitInfosFilePath = Path.Combine(outDir.FullName, unitInfos.FileName);
            File.WriteAllText(unitInfosFilePath, unitInfos.TransformText());
        }
    }
}
