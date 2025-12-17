namespace Dtdl2Wot
{
    using System.Linq;

    /// <summary>
    /// Static class that defines designators used to identify payload formats.
    /// </summary>
    public static class PayloadFormat
    {
        public const string Avro = "Avro/1.11.0";

        public const string Json = "Json/ecma/404";

        public const string Raw = "raw/0";

        public const string Custom = "custom/0";

        public static string Itemize(string separator, string mark) =>
            string.Join(separator, new string[]
            {
                Avro,
                Json,
                Raw,
                Custom,
            }.Select(s => $"{mark}{s}{mark}"));
    }
}
