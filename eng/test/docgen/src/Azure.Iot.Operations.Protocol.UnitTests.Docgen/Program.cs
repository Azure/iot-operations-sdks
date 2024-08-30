namespace Azure.Iot.Operations.Protocol.Docgen
{
    internal class Program
    {
        private const string SchemaFolderPath = @"../../../../lib/test-cases/schemas";
        private const string TestCaseRoot = @"../../../../lib/test-cases/Protocol";
        private const string TestCaseDocFilePath = @"../../../../docs/specs/MetlCases.md";
        private const string TestCaseProtoDocFilePath = @"../../../../docs/proto/MetlCasesProto.xml";
        private const string LanguageDocFilePath = @"../../../../docs/specs/MetlSpec.md";
        private const string LanguageProtoDocFilePath = @"../../../../docs/proto/MetlSpecProto.xml";

        static void Main()
        {
            using JsonSchemata jsonSchemata = new JsonSchemata(SchemaFolderPath);
            DefaultValues defaultValues = new DefaultValues(TestCaseRoot);
            ExampleCatalog exampleCatalog = new ExampleCatalog(TestCaseRoot);
            TestCaseCatalog testCaseCatalog = new TestCaseCatalog(TestCaseRoot);

//            TestCaseDocumenter testCaseDocumenter = new TestCaseDocumenter(TestCaseRoot, TestCaseDocFilePath);
//            testCaseDocumenter.ProduceDocument();

            DocumentGenerator testCaseDocGenerator = new DocumentGenerator(TestCaseProtoDocFilePath, jsonSchemata, defaultValues, exampleCatalog, testCaseCatalog);
            testCaseDocGenerator.ProduceDocument(TestCaseDocFilePath);

            CompletenessChecker languageCompletenessChecker = new(jsonSchemata.GetSchemaNames());
            DocumentGenerator languageDocGenerator = new DocumentGenerator(LanguageProtoDocFilePath, jsonSchemata, defaultValues, exampleCatalog, testCaseCatalog, languageCompletenessChecker);
            languageDocGenerator.ProduceDocument(LanguageDocFilePath);
            languageCompletenessChecker.CheckCompleteness();
        }
    }
}
