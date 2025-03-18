// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol
{
    public static class GuidExtensions
    {
        public static bool TryParseBytes(byte[] bytes, out Guid? result)
        {
            result = null!;
            if (bytes == null || bytes.Length != 16)
            {
                return false;
            }
            try
            {
                result = new Guid(bytes);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceInformation(ex.Message);
                return false;
            }
        }
    }
}
