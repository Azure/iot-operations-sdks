// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Help;
    using System.CommandLine.Invocation;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    internal class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction? defaultHelpAction;

        public CustomHelpAction(CommandLineAction? action)
        {
            defaultHelpAction = action as HelpAction;
        }

        public override int Invoke(ParseResult parseResult)
        {
            int result = defaultHelpAction?.Invoke(parseResult) ?? 1;

            foreach (string lang in CommandPerformer.LanguageMap.Keys)
            {
                LanguageInfo languageInfo = CommandPerformer.LanguageMap[lang];
                Console.WriteLine($"For --lang {lang}, the --namespace and --common options {languageInfo.ArgConstraint}");
            }

            return result;
        }
    }
}
