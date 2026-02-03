// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    public class OpcUaName
    {
        public OpcUaName(string nameString)
        {
            (NsIndex, Name) = UaUtil.ParseNameString(nameString);
        }

        public int NsIndex { get; }

        public string Name { get; }
    }
}
