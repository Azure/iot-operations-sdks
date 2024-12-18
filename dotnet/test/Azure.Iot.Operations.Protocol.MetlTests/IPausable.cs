// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System.Threading.Tasks;

    public interface IPausable
    {
        bool HasFired { get; }

        Task<bool> TryPauseAsync();

        Task ResumeAsync();
    }
}
