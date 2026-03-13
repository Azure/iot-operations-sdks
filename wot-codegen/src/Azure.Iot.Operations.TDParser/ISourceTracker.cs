// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    public interface ISourceTracker : ITraversable
    {
        bool DeserializingFailed { get; }

        string? DeserializationError { get; }

        long TokenIndex { get; }
    }
}
    