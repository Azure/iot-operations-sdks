// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;

    public class SpecMapper
    {
        private const string modelUriPrefix = "http://opcfoundation.org/UA/";

        private static readonly List<(string, string)> mapping = new()
        {
            ( "http://opcfoundation.org/UA/", "OpcUaCore" ),
            ( "http://fdi-cooperation.com/OPCUA/FDI5/", "FDI5" ),
            ( "http://fdi-cooperation.com/OPCUA/FDI7/", "FDI7" ),
            ( "http://opcfoundation.org/UA/AML/", "AMLBaseTypes" ),
            ( "http://opcfoundation.org/UA/Dictionary/IRDI", "IRDI" ),
            ( "http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/", "isa95-jobcontrol" ),
            ( "http://sercos.org/UA/", "sercos" ),
            ( "http://vdma.org/UA/LaserSystem-Example/", "LaserSystem-Example" ),
            ( "http://www.OPCFoundation.org/UA/2013/01/ISA95", "ISA95" )
        };

        private static readonly Dictionary<string, string> UriToSpecName = mapping.ToDictionary(e => e.Item1, e => e.Item2);

        public static string GetSpecNameFromUri(string modelUri)
        {
            if (UriToSpecName.TryGetValue(modelUri, out string? specName))
            {
                return specName;
            }
            else
            {
                return modelUri.Substring(modelUriPrefix.Length, modelUri.Length - modelUriPrefix.Length - (modelUri.EndsWith('/') ? 1 : 0)).Replace('/', '.');
            }
        }
    }
}
