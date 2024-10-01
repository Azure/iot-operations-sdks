using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    internal class FileUtilities
    {
        internal static byte[] ReadFileWithRetry(string path)
        {
            int retryCount = 0;
            while (true)
            {
                retryCount++;

                try
                {
                    byte[] contents = File.ReadAllBytes(path);
                    return contents;
                }
                catch (IOException)
                {
                    if (retryCount > 10)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
