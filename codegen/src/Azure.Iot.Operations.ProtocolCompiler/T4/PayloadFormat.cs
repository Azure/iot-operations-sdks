namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Linq;

    /// <summary>
    /// Static class that defines designators used to identify payload formats.
    /// </summary>
    public static class PayloadFormat
    {
        public const string Avro = "Avro/1.11.0";

        public const string Cbor = "Cbor/rfc/8949";

        public const string Json = "Json/ecma/404";

        public const string Proto2 = "Protobuf/2";

        public const string Proto3 = "Protobuf/3";

        public const string Raw = "raw/0";

        public const string Custom = "custom/0";

        public static string Itemize(string separator, string mark) =>
            string.Join(separator, new string[]
            {
                Avro,
                Cbor,
                Json,
                Proto2,
                Proto3,
                Raw,
                Custom,
            }.Select(s => $"{mark}{s}{mark}"));
    }
}
