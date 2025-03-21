namespace Azure.Iot.Operations.ProtocolCompiler
{
    public interface ITypeName
    {
        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, bool local = false);

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null);
    }
}
