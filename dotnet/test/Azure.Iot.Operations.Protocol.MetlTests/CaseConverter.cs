// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public static class CaseConverter
    {
        public static string PascalToKebabCase(string name)
        {
            StringBuilder builder = new();
            try
            {
                char c = '\0';
                foreach (char c2 in name)
                {
                    if (char.IsUpper(c2) && !char.IsUpper(c) && c != 0 && c != '-')
                    {
                        builder.Append('-');
                    }

                    builder.Append(char.ToLowerInvariant(c2));
                    c = c2;
                }

                return builder.ToString();
            }
            finally
            {
                builder.Length = 0;
            }
        }
    }
}
