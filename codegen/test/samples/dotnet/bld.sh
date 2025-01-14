set -e

dotnet build ./CommandVariantsSample
dotnet build ./CommandComplexSchemasSample
dotnet build ./CommandRawSample
dotnet build ./TelemetryAndCommandSample
dotnet build ./TelemetryAndCommandSampleClientOnly
dotnet build ./TelemetryAndCommandSampleServerOnly
dotnet build ./TelemetryComplexSchemasSample
dotnet build ./TelemetryPrimitiveSchemasSample
dotnet build ./TelemetryRawSingleSample
dotnet build ./TelemetryRawSeparateSample
