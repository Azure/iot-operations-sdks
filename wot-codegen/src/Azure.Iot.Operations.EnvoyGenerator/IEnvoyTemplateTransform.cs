// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    internal interface IEnvoyTemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        EndpointTarget EndpointTarget { get; }

        string TransformText();
    }
}
