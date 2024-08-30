// <copyright file="NameFormatter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using System.Linq;
    using DTDLParser;

    /// <summary>
    /// Static class that formats information from DTDL models as various C# element names.
    /// </summary>
    public static class NameFormatter
    {
        /// <summary>
        /// Format an Interface identifier as a namespace name.
        /// </summary>
        /// <param name="dtmi">The identifier of a DTDL Interface.</param>
        /// <returns>The namespace to be used for code generation.</returns>
        public static string DtmiToNamespace(Dtmi dtmi)
        {
            return GetLanguageSafeString(dtmi.AbsoluteUri);
        }

        /// <summary>
        /// Format an string to be language safe (no special characters, only underscores).
        /// </summary>
        /// <param name="languageUnsafeString">The language-unsafe string (e.g., a DTMI).</param>
        /// <returns>A safe string to be used for code generation.</returns>
        public static string GetLanguageSafeString(string languageUnsafeString)
        {
            return languageUnsafeString.Replace("_", "__").Replace(":", "_").Replace(";", "__").Replace(".", "_");
        }

        /// <summary>
        /// Format an Interface identifier as a service name.
        /// </summary>
        /// <param name="dtmi">The identifier of a DTDL Interface.</param>
        /// <returns>The name to use for the generated service.</returns>
        public static string DtmiToServiceName(Dtmi dtmi)
        {
            return Capitalize(dtmi.Labels.Last());
        }

        /// <summary>
        /// Format an identifier as a schema name.
        /// </summary>
        /// <param name="dtmi">The identifier of a DTDL schema element.</param>
        /// <param name="interfaceId">Identifier of the DTDL Interface in which the schema element is defined.</param>
        /// <param name="schemaKind">A string representation of the kind of schema.</param>
        /// <returns>The name by which to refer to the schema.</returns>
        public static string DtmiToSchemaName(Dtmi dtmi, Dtmi interfaceId, string schemaKind)
        {
            List<string> seq = new List<string>() { schemaKind };

            if (dtmi.IsReserved)
            {
                seq.AddRange(dtmi.Labels.TakeLast(dtmi.Labels.Length - interfaceId.Labels.Length));

                return string.Join('_', seq
                    .Where(l => l != "_contents" && l != "_schema" && l != "_fields")
                    .Select(l => l.StartsWith("__") ? l.Substring(2) : l)
                    .Select(l => l.StartsWith('_') ? l.Substring(1) : l)
                    .Select(l => Capitalize(l)));
            }
            else
            {
                seq.AddRange(dtmi.Labels);
                seq.Add($"_{dtmi.MajorVersion}");
                if (dtmi.MinorVersion > 0)
                {
                    seq.Add(dtmi.MinorVersion.ToString());
                }

                return string.Join('_', seq.Select(l => Capitalize(l)));
            }
        }

        public static string Capitalize(string inString)
        {
            return char.ToUpperInvariant(inString[0]) + inString.Substring(1);
        }
    }
}
