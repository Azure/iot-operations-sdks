// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;

    public record LanguageInfo(TargetLanguage TargetLanguage, string SrcSubdir, Regex ArgRegex, bool NamespaceRequired, bool CommonRequired, string DefaultNamespace, string DefaultCommon, string ArgConstraint);
}
