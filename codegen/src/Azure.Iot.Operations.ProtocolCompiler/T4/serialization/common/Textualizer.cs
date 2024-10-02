namespace Azure.Iot.Operations.ProtocolCompiler
{
    public static class Textualizer
    {
        public static string Capitalize(string inString)
        {
            return char.ToUpperInvariant(inString[0]) + inString.Substring(1);
        }
    }
}
