// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    public record ErrorSpec(string SchemaName, string Description, string? MessageField, bool MessageIsRequired, string? ErrorCodeName = null, string? ErrorCodeSchema = null, string? ErrorInfoName = null, string? ErrorInfoSchema = null);
}
