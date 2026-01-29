// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class MultiCodeName
    {
        private readonly List<CodeName> nameComponents;

        public MultiCodeName(string givenName = "")
        {
            nameComponents = givenName.Split('.').Select(c => new CodeName(c)).ToList();
        }

        public string GetNamespaceName(TargetLanguage language) => string.Join('.', nameComponents.Select(c => c.GetTypeName(language)));

        public string GetFolderName(TargetLanguage language) => string.Join('.', nameComponents.Select(c => c.GetFolderName(language)));
    }
}
