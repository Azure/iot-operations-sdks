namespace SpecMapper
{
    using System.Collections.Generic;
    using System.Linq;

    public static class SpecMapper
    {
        private const string modelUriPrefix = "http://opcfoundation.org/UA/";

        private static readonly List<(string, string)> mapping = new()
        {
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
        private static readonly Dictionary<string, string> SpecNameToUri = mapping.ToDictionary(e => e.Item2, e => e.Item1);

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

        public static string GetUriFromSpecName(string? specName)
        {
            if (specName == null)
            {
                return modelUriPrefix.Substring(0, modelUriPrefix.Length - 1);
            }
            else if (SpecNameToUri.TryGetValue(specName, out string? uri))
            {
                return uri.EndsWith('/') ? uri.Substring(0, uri.Length - 1) : uri;
            }
            else
            {
                return $"{modelUriPrefix}{specName}";
            }
        }
    }
}
