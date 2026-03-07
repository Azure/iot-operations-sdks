// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    internal class CommandHandler
    {
        private static UnitReader unitReader;
        private static readonly Dictionary<string, Dictionary<TableKind, List<ITemplateTransform>>> languageMap;

        public static readonly string[] SupportedLanguages;

        static CommandHandler()
        {
            unitReader = new UnitReader();
            languageMap = new()
            {
                {
                    "rust",
                    new Dictionary<TableKind, List<ITemplateTransform>>
                    {
                        {
                            TableKind.Conversion,
                            new List<ITemplateTransform>
                            {
                                new RustEceCodes(unitReader.EceCodesMap),
                                new RustUnitInfos(unitReader.UnitInfosMap)
                            }
                        },
                        {
                            TableKind.Selection,
                            new List<ITemplateTransform>
                            {
                                new RustKindLabeledUnits(unitReader.KindLabeledUnitsMap),
                                new RustKindSystemUnits(unitReader.KindSystemUnitsMap),
                                new RustLabeledSystemsOfUnits(unitReader.LabeledSystemsOfUnits)
                            }
                        },
                    }
                },
            };

            SupportedLanguages = languageMap.Keys.ToArray();
        }

        public static int PopulateTables(OptionContainer options)
        {
            if (!options.OutputDir.Exists)
            {
                options.OutputDir.Create();
            }

            if (!languageMap.TryGetValue(options.Language.ToLowerInvariant(), out Dictionary<TableKind, List<ITemplateTransform>>? transforms))
            {
                Console.WriteLine($"Language not recognized: '{options.Language}'; language must be {string.Join(" or ", languageMap.Keys.Select(l => $"'{l}'"))}");
                return 1;
            }

            foreach (var transform in transforms[options.TableKind])
            {
                string filePath = Path.Combine(options.OutputDir.FullName, transform.FileName);
                File.WriteAllText(filePath, transform.TransformText());
            }

            return 0;
        }
    }
}
