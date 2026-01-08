namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests
{
    using Xunit;
    using System.Buffers;
    using System.IO;
    using System.Text.Json;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    public class ProtocolCompilerTester
    {
        private const string basePath = "../../..";
        private const string testCasesPath = $"{basePath}/test-cases";
        private const string successCasesPath = $"{testCasesPath}/success";
        private const string failureCasesPath = $"{testCasesPath}/failure";
        private const string tmPath = $"{basePath}/thing-models";
        private const string schemasPath = $"{basePath}/schemas";
        private const string namerPath = $"{basePath}/name-config";
        private const string sandboxPath = $"{basePath}/sandbox";

        static ProtocolCompilerTester()
        {
        }

        public static IEnumerable<object[]> GetFailureTestCases()
        {
            foreach (string testCasePath in Directory.GetFiles(failureCasesPath, @"*.json"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                yield return new object[] { testCaseName };
            }
        }

        public static IEnumerable<object[]> GetSuccessTestCases()
        {
            foreach (string testCasePath in Directory.GetFiles(successCasesPath, @"*.json"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                yield return new object[] { testCaseName };
            }
        }

        [Theory]
        [MemberData(nameof(GetFailureTestCases))]
        public async Task TestProtocolCompilerFailures(string testCaseName)
        {
            TestCase? testCase;
            using (StreamReader streamReader = File.OpenText($"{failureCasesPath}/{testCaseName}.json"))
            {
                testCase = JsonSerializer.Deserialize<TestCase>(streamReader.ReadToEnd());
                Assert.False(testCase == null, $"Test case '{testCaseName}' descriptor failed to deserialize");
                Assert.False(testCase.Success, $"Test case '{testCaseName}' is in failure folder but is marked as success.");
                Assert.False(testCase.Errors.Length == 0, $"Test case '{testCaseName}' is marked as failure but descriptor contains no 'errors' elements.");
            }

            OptionContainer options = GetOptionContainer(testCaseName, testCase.CommandLine);

            if (options.OutputDir.Exists)
            {
                options.OutputDir.Delete(recursive: true);
            }

            ErrorLog errorLog = CommandPerformer.GenerateCode(options, (_, _) => { }, suppressExternalTools: true);

            if (errorLog.HasErrors)
            {
                if (errorLog.FatalError != null)
                {
                    CheckError(testCaseName, errorLog.FatalError, testCase.Errors);
                }

                foreach (ErrorRecord errorRecord in errorLog.Errors)
                {
                    CheckError(testCaseName, errorRecord, testCase.Errors);
                }
            }
            else
            {
                Assert.Fail($"Test case '{testCaseName}' was expected to fail but returned no errors.");
            }
        }

        [Theory]
        [MemberData(nameof(GetSuccessTestCases))]
        public async Task TestProtocolCompilerSuccesses(string testCaseName)
        {
            TestCase? testCase;
            using (StreamReader streamReader = File.OpenText($"{successCasesPath}/{testCaseName}.json"))
            {
                testCase = JsonSerializer.Deserialize<TestCase>(streamReader.ReadToEnd());
                Assert.False(testCase == null, $"Test case '{testCaseName}' descriptor failed to deserialize");
                Assert.True(testCase.Success, $"Test case '{testCaseName}' is in success folder but is marked as failure.");
                Assert.True(testCase.Errors.Length == 0, $"Test case '{testCaseName}' is marked as success but descriptor contains 'errors' elements.");
            }

            OptionContainer options = GetOptionContainer(testCaseName, testCase.CommandLine);

            if (options.OutputDir.Exists)
            {
                options.OutputDir.Delete(recursive: true);
            }

            ErrorLog errorLog = CommandPerformer.GenerateCode(options, (_, _) => { }, suppressExternalTools: true);

            if (errorLog.HasErrors)
            {
                if (errorLog.FatalError != null)
                {
                    Assert.Fail($"Test case '{testCaseName}' was expected to succeed but returned fatal error: '{errorLog.FatalError.Message}', file: {errorLog.FatalError.Filename}, line: {errorLog.FatalError.LineNumber}");
                }
                else
                {
                    Assert.Fail($"Test case '{testCaseName}' was expected to succeed but returned {errorLog.Errors.Count} error(s) including: '{errorLog.Errors.First().Message}', file: {errorLog.Errors.First().Filename}, line: {errorLog.Errors.First().LineNumber}");
                }
            }
        }

        private static void CheckError(string testCaseName, ErrorRecord errorRecord, TestError[] errors)
        {
            TestError? expectedError = GetBestMatchingExpectedError(errorRecord, errors);
            if (expectedError != null)
            {
                if (!Enum.TryParse<ErrorCondition>(expectedError.Condition, out ErrorCondition expectedCondition))
                {
                    Assert.Fail($"Test case '{testCaseName}' contains invalid error condition string '{expectedError.Condition}' in expected errors.");
                }

                Assert.True(expectedCondition == errorRecord.Condition, $"Test case '{testCaseName}' returned error with unexpected condition. Expected: '{expectedCondition}', Actual: '{errorRecord.Condition}'; Message: \"{errorRecord.Message}\"");
                Assert.True(expectedError.Filename == errorRecord.Filename, $"Test case '{testCaseName}' returned error with unexpected filename. Expected: '{expectedError.Filename}', Actual: '{errorRecord.Filename}'; Message: \"{errorRecord.Message}\"");
                Assert.True(expectedError.LineNumber == errorRecord.LineNumber, $"Test case '{testCaseName}' returned error with unexpected line number. Expected: {expectedError.LineNumber}, Actual: {errorRecord.LineNumber}; Message: \"{errorRecord.Message}\"");
                Assert.True(expectedError.CfLineNumber == errorRecord.CfLineNumber, $"Test case '{testCaseName}' returned error with unexpected cfLine number. Expected: {expectedError.CfLineNumber}, Actual: {errorRecord.CfLineNumber}; Message: \"{errorRecord.Message}\"");
                Assert.True(expectedError.CrossRef == errorRecord.CrossRef, $"Test case '{testCaseName}' returned error with unexpected crossRef. Expected: '{expectedError.CrossRef}', Actual: '{errorRecord.CrossRef}'; Message: \"{errorRecord.Message}\"");
            }
            else
            {
                Assert.Fail($"Test case '{testCaseName}' returned unexpected error: '{errorRecord.Message}', file: {errorRecord.Filename}, line: {errorRecord.LineNumber}, cfLine: {errorRecord.CfLineNumber}, crossRef: '{errorRecord.CrossRef}'");
            }
        }

        private static TestError? GetBestMatchingExpectedError(ErrorRecord errorRecord, TestError[] errors)
        {
            if (errors.Length == 0)
            {
                return null;
            }

            List<TestError> fileMatches = errors.Where(e => e.Filename == errorRecord.Filename).ToList();
            if (fileMatches.Count == 0)
            {
                return errors[0];
            }

            List<TestError> conditionMatches = fileMatches.Where(e => e.Condition == errorRecord.Condition.ToString()).ToList();
            if (conditionMatches.Count == 0)
            {
                return fileMatches[0];
            }

            int minLineOffset = conditionMatches.Min(e => Math.Abs(e.LineNumber - errorRecord.LineNumber));
            List<TestError> minOffsetMatches = conditionMatches.Where(e => Math.Abs(e.LineNumber - errorRecord.LineNumber) == minLineOffset).ToList();

            int minCfLineOffset = minOffsetMatches.Min(e => Math.Abs(e.CfLineNumber - errorRecord.CfLineNumber));
            List<TestError> minCfOffsetMatches = minOffsetMatches.Where(e => Math.Abs(e.CfLineNumber - errorRecord.CfLineNumber) == minCfLineOffset).ToList();

            return minCfOffsetMatches.First();
        }

        private static OptionContainer GetOptionContainer(string testCaseName, TestCommandLine commandLine)
        {
            string testCaseSandboxPath = $"{sandboxPath}/{testCaseName}";

            if (commandLine.ThingFiles.Any(tf => Path.IsPathRooted(tf)))
            {
                Assert.Fail($"Test case '{testCaseName}' specifies absolute path for thing file, which is not supported in test.");
            }
            if (commandLine.SchemaFiles.Any(sf => Path.IsPathRooted(sf)))
            {
                Assert.Fail($"Test case '{testCaseName}' specifies absolute path for schema file, which is not supported in test.");
            }
            if (commandLine.TypeNamerFile != null && Path.IsPathRooted(commandLine.TypeNamerFile))
            {
                Assert.Fail($"Test case '{testCaseName}' specifies absolute path for type namer file, which is not supported in test.");
            }
            if (commandLine.OutputDir != null && Path.IsPathRooted(commandLine.OutputDir))
            {
                Assert.Fail($"Test case '{testCaseName}' specifies absolute path for output directory, which is not supported in test.");
            }
            if (commandLine.WorkingDir != null && Path.IsPathRooted(commandLine.WorkingDir))
            {
                Assert.Fail($"Test case '{testCaseName}' specifies absolute path for working directory, which is not supported in test.");
            }

            FileInfo[] thingFiles = commandLine.ThingFiles.Select(tf => new FileInfo(Path.GetFullPath($"{tmPath}/{tf}"))).ToArray();
            string[] schemaFiles = commandLine.SchemaFiles.Select(tf => Path.GetFullPath($"{schemasPath}/{tf}")).ToArray();
            FileInfo? typeNamerFile = commandLine.TypeNamerFile != null ? new FileInfo(Path.GetFullPath($"{namerPath}/{commandLine.TypeNamerFile}")) : null;
            DirectoryInfo outputDir = new DirectoryInfo(commandLine.OutputDir != null ? Path.GetFullPath($"{testCaseSandboxPath}/{commandLine.OutputDir}") : $"{testCaseSandboxPath}/{CommandPerformer.DefaultOutDir}");
            DirectoryInfo workingDir = new DirectoryInfo(commandLine.WorkingDir != null ? Path.GetFullPath($"{outputDir}/{commandLine.WorkingDir}") : $"{outputDir}/{CommandPerformer.DefaultWorkingDir}");
            string genNamespace = commandLine.GenNamespace ?? CommandPerformer.DefaultNamespace;

            return new OptionContainer
            {
                ThingFiles = thingFiles,
                SchemaFiles = schemaFiles,
                TypeNamerFile = typeNamerFile,
                OutputDir = outputDir,
                WorkingDir = workingDir,
                GenNamespace = genNamespace,
                SdkPath = commandLine.SdkPath,
                Language = commandLine.Language ?? "none",
                ClientOnly = commandLine.ClientOnly,
                ServerOnly = commandLine.ServerOnly,
                NoProj = commandLine.NoProj,
                DefaultImpl = commandLine.DefaultImpl
            };
        }
    }
}
