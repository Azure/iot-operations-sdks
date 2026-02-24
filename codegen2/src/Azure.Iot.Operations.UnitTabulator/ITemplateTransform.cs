// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string TransformText();
    }
}
