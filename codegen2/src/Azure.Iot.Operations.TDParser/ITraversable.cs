// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    using System.Collections.Generic;

    public interface ITraversable
    {
        IEnumerable<ITraversable> Traverse();
    }
}
