namespace Azure.Iot.Operations.ProtocolCompiler
{
    public abstract class EmptyTypeName: ITypeName
    {
        public static EmptyAvroTypeName AvroInstance = new();
        public static EmptyCborTypeName CborInstance = new();
        public static EmptyJsonTypeName JsonInstance = new();
        public static EmptyProtoTypeName ProtoInstance = new();
        public static EmptyRawTypeName RawInstance = new();
        public static EmptyCustomTypeName CustomInstance = new();

        public class EmptyAvroTypeName : EmptyTypeName
        {
            public override string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.CSharp) => "EmptyAvro",
                (null, null, null, TargetLanguage.Rust) => "EmptyAvro",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyAvroTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(EmptyAvroTypeName)}"),
            };

            public override string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.Rust) => "empty_avro",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyAvroTypeName)} cannot take a suffix" : $"There is no {language} file name for {typeof(EmptyAvroTypeName)}"),
            };

            public override string GetAllocator(TargetLanguage language) => language switch
            {
                TargetLanguage.CSharp => "new EmptyAvro()",
                TargetLanguage.Rust => "EmptyAvro{}",
                _ => throw new InvalidOperationException($"There is no {language} allocator for {typeof(EmptyAvroTypeName)}"),
            };
        }

        public class EmptyCborTypeName : EmptyTypeName
        {
            public override string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.CSharp) => "EmptyCbor",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyCborTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(EmptyCborTypeName)}"),
            };

            public override string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyCborTypeName)} cannot take a suffix" : $"There is no {language} file name for {typeof(EmptyCborTypeName)}"),
            };

            public override string GetAllocator(TargetLanguage language) => language switch
            {
                TargetLanguage.CSharp => "new EmptyCbor()",
                _ => throw new InvalidOperationException($"There is no {language} allocator for {typeof(EmptyAvroTypeName)}"),
            };
        }

        public class EmptyJsonTypeName : EmptyTypeName
        {
            public override string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.CSharp) => "EmptyJson",
                (null, null, null, TargetLanguage.Rust) => "EmptyJson",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyJsonTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(EmptyJsonTypeName)}"),
            };

            public override string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.Rust) => "empty_json",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyJsonTypeName)} cannot take a suffix" : $"There is no {language} file name for {typeof(EmptyJsonTypeName)}"),
            };

            public override string GetAllocator(TargetLanguage language) => language switch
            {
                TargetLanguage.CSharp => "new EmptyJson()",
                TargetLanguage.Rust => "EmptyJson{}",
                _ => throw new InvalidOperationException($"There is no {language} allocator for {typeof(EmptyAvroTypeName)}"),
            };
        }

        public class EmptyProtoTypeName : EmptyTypeName
        {
            public override string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.CSharp) => "Google.Protobuf.WellKnownTypes.Empty",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyProtoTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(EmptyProtoTypeName)}"),
            };

            public override string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyProtoTypeName)} cannot take a suffix" : $"There is no {language} file name for {typeof(EmptyProtoTypeName)}"),
            };

            public override string GetAllocator(TargetLanguage language) => language switch
            {
                TargetLanguage.CSharp => "new Google.Protobuf.WellKnownTypes.Empty()",
                _ => throw new InvalidOperationException($"There is no {language} allocator for {typeof(EmptyAvroTypeName)}"),
            };
        }

        public class EmptyRawTypeName : EmptyTypeName
        {
            public override string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.CSharp) => "byte[]",
                (null, null, null, TargetLanguage.Go) => "[]byte",
                (null, null, null, TargetLanguage.Rust) => "byte[]",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyRawTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(EmptyRawTypeName)}"),
            };

            public override string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) =>
                throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyRawTypeName)} cannot take a suffix" : $"There is no {language} file name for {typeof(EmptyRawTypeName)}");

            public override string GetAllocator(TargetLanguage language) => language switch
            {
                TargetLanguage.CSharp => "Array.Empty<byte>()",
                TargetLanguage.Go => "[]byte{}",
                TargetLanguage.Rust => "byte[]{}",
                _ => throw new InvalidOperationException($"There is no {language} allocator for {typeof(EmptyAvroTypeName)}"),
            };
        }

        public class EmptyCustomTypeName : EmptyTypeName
        {
            public override string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
            {
                (null, null, null, TargetLanguage.CSharp) => "CustomPayload",
                (null, null, null, TargetLanguage.Go) => "protocol.Data",
                (null, null, null, TargetLanguage.Rust) => "CustomPayload",
                _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyCustomTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(EmptyCustomTypeName)}"),
            };

            public override string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) =>
                throw new InvalidOperationException(suffix1 != null ? $"{typeof(EmptyCustomTypeName)} cannot take a suffix" : $"There is no {language} file name for {typeof(EmptyCustomTypeName)}");

            public override string GetAllocator(TargetLanguage language) => language switch
            {
                TargetLanguage.CSharp => "ExternalSerializer.EmptyValue",
                TargetLanguage.Go => "protocol.Data{}",
                TargetLanguage.Rust => "CustomPayload{}",
                _ => throw new InvalidOperationException($"There is no {language} allocator for {typeof(EmptyAvroTypeName)}"),
            };
        }

        public abstract string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null);

        public abstract string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null);

        public abstract string GetAllocator(TargetLanguage language);
    }
}
