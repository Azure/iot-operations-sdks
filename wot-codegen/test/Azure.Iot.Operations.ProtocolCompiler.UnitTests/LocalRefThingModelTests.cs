// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.ProtocolCompilerLib;
    using Xunit;

    public class LocalRefThingModelTests
    {
        private static readonly string BasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        private static readonly string ThingModelsPath = Path.Combine(BasePath, "thing-models");
        private static readonly string SandboxPath = Path.Combine(BasePath, "sandbox");

        [Fact]
        public void LocalRefsGenerateSchemasAndDoNotWarnAboutUsage()
        {
            OptionContainer options = CreateOptions("LocalRefSchemas", "valid/LocalRefSchemas.TM.json", language: "csharp", sdkTarget: "aio");

            ErrorLog errorLog = CommandPerformer.GenerateCode(options, (_, _) => { }, suppressExternalTools: true);

            Assert.False(errorLog.HasErrors, FormatErrors(errorLog));
            Assert.Empty(errorLog.Warnings);

            string propertySchemaPath = Path.Combine(options.WorkingDir.FullName, "DisplayNameProperty.schema.json");
            string temperatureSchemaPath = Path.Combine(options.WorkingDir.FullName, "TemperatureProperty.schema.json");
            string widgetSchemaPath = Path.Combine(options.WorkingDir.FullName, "Widget.schema.json");
            string actionOnlyWidgetSchemaPath = Path.Combine(options.WorkingDir.FullName, "ActionOnlyWidget.schema.json");
            string actionInputSchemaPath = Path.Combine(options.WorkingDir.FullName, "UpdateWidgetInputArguments.schema.json");
            string eventSchemaPath = Path.Combine(options.WorkingDir.FullName, "WidgetUpdatedEvent.schema.json");

            Assert.True(File.Exists(propertySchemaPath), $"Missing generated schema '{propertySchemaPath}'.");
            Assert.True(File.Exists(temperatureSchemaPath), $"Missing generated schema '{temperatureSchemaPath}'.");
            Assert.True(File.Exists(widgetSchemaPath), $"Missing generated schema '{widgetSchemaPath}'.");
            Assert.True(File.Exists(actionOnlyWidgetSchemaPath), $"Missing generated schema '{actionOnlyWidgetSchemaPath}'.");
            Assert.True(File.Exists(actionInputSchemaPath), $"Missing generated schema '{actionInputSchemaPath}'.");
            Assert.True(File.Exists(eventSchemaPath), $"Missing generated schema '{eventSchemaPath}'.");
            Assert.True(File.Exists(Path.Combine(options.OutputDir.FullName, "Generated", "TerminalError.g.cs")));

            using JsonDocument propertySchema = JsonDocument.Parse(File.ReadAllText(propertySchemaPath));
            JsonElement propertyDefinition = propertySchema.RootElement.GetProperty("properties").GetProperty("displayName");
            Assert.Equal("string", propertyDefinition.GetProperty("type").GetString());

            using JsonDocument temperatureSchema = JsonDocument.Parse(File.ReadAllText(temperatureSchemaPath));
            JsonElement temperatureDefinition = temperatureSchema.RootElement.GetProperty("properties").GetProperty("temperature");
            Assert.Equal("number", temperatureDefinition.GetProperty("type").GetString());

            using JsonDocument widgetSchema = JsonDocument.Parse(File.ReadAllText(widgetSchemaPath));
            JsonElement widgetProperties = widgetSchema.RootElement.GetProperty("properties");
            Assert.Equal("string", widgetProperties.GetProperty("name").GetProperty("type").GetString());
            Assert.Equal("boolean", widgetProperties.GetProperty("enabled").GetProperty("type").GetString());
            Assert.Contains("name", widgetSchema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()));

            using JsonDocument actionInputSchema = JsonDocument.Parse(File.ReadAllText(actionInputSchemaPath));
            Assert.Equal("object", actionInputSchema.RootElement.GetProperty("type").GetString());
            Assert.EndsWith("ActionOnlyWidget.schema.json", actionInputSchema.RootElement.GetProperty("$ref").GetString(), StringComparison.Ordinal);

            using JsonDocument eventSchema = JsonDocument.Parse(File.ReadAllText(eventSchemaPath));
            Assert.EndsWith("Widget.schema.json", eventSchema.RootElement.GetProperty("properties").GetProperty("widgetUpdated").GetProperty("$ref").GetString(), StringComparison.Ordinal);

            string generatedService = File.ReadAllText(Path.Combine(options.OutputDir.FullName, "Generated", "LocalRefThing.g.cs"));
            Assert.Contains("Output = extended.Response,", generatedService, StringComparison.Ordinal);
            Assert.Contains("Response = extended.Response.Output!", generatedService, StringComparison.Ordinal);
            Assert.Contains("TerminalError", generatedService, StringComparison.Ordinal);
            Assert.DoesNotContain("IgnoredInputTitle", generatedService, StringComparison.Ordinal);
            Assert.DoesNotContain("IgnoredOutputTitle", generatedService, StringComparison.Ordinal);
            Assert.DoesNotContain("ErrorAlias", generatedService, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("PropertyLocalRefMissingTarget", "invalidAioBinding/PropertyLocalRefMissingTarget.TM.json", ErrorCondition.ItemNotFound, "PropertyLocalRefMissingTarget.TM.json", 17)]
        [InlineData("PropertyLocalRefMalformedPath", "invalidAioBinding/PropertyLocalRefMalformedPath.TM.json", ErrorCondition.PropertyInvalid, "PropertyLocalRefMalformedPath.TM.json", 12)]
        [InlineData("PropertyLocalRefWithType", "invalidAioBinding/PropertyLocalRefWithType.TM.json", ErrorCondition.ValuesInconsistent, "PropertyLocalRefWithType.TM.json", 18)]
        [InlineData("SchemaDefinitionLocalRefCycle", "invalidAioBinding/SchemaDefinitionLocalRefCycle.TM.json", ErrorCondition.Interminable, "SchemaDefinitionLocalRefCycle.TM.json", 13)]
        public void InvalidLocalRefsReturnFocusedValidationErrors(string testName, string thingFile, ErrorCondition expectedCondition, string expectedFilename, int expectedLineNumber)
        {
            OptionContainer options = CreateOptions(testName, thingFile, language: "none");

            ErrorLog errorLog = CommandPerformer.GenerateCode(options, (_, _) => { }, suppressExternalTools: true);

            Assert.True(errorLog.HasErrors, $"Expected '{testName}' to fail validation.");
            Assert.Null(errorLog.FatalError);

            ErrorRecord error = Assert.Single(errorLog.Errors);
            Assert.Equal(expectedCondition, error.Condition);
            Assert.Equal(expectedFilename, error.Filename);
            Assert.Equal(expectedLineNumber, error.LineNumber);
        }

        private static OptionContainer CreateOptions(string testName, string thingFile, string language, string sdkTarget = "none")
        {
            DirectoryInfo outputDir = new(Path.Combine(SandboxPath, testName));
            DirectoryInfo workingDir = new(Path.Combine(outputDir.FullName, CommandPerformer.DefaultWorkingDir));

            if (outputDir.Exists)
            {
                outputDir.Delete(recursive: true);
            }

            return new OptionContainer
            {
                ThingFiles = [new FileInfo(Path.GetFullPath(Path.Combine(ThingModelsPath, thingFile)))],
                ClientThingFiles = [],
                ServerThingFiles = [],
                SchemaFiles = [],
                TypeNamerFile = null,
                OutputDir = outputDir,
                WorkingDir = workingDir,
                GenNamespace = null,
                CommonNamespace = null,
                Language = language,
                SdkTarget = sdkTarget,
            };
        }

        private static string FormatErrors(ErrorLog errorLog)
        {
            if (errorLog.FatalError != null)
            {
                return $"Fatal: {errorLog.FatalError.Message} ({errorLog.FatalError.Filename}:{errorLog.FatalError.LineNumber})";
            }

            if (errorLog.Errors.Count == 0)
            {
                return "No errors were reported.";
            }

            return string.Join("; ", errorLog.Errors.Select(error => $"{error.Condition}: {error.Message} ({error.Filename}:{error.LineNumber})"));
        }
    }
}
