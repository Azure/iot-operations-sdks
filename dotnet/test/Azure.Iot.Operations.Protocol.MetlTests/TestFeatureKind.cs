// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public enum TestFeatureKind
    {
        Unobtanium = 0,
        AckOrdering = 1,
        TopicFiltering = 2,
        Reconnection = 3,
        Caching = 4,
        Dispatch = 5,
        ExplicitDefault = 6,
        MultipleSerializers = 7,
    }
}
