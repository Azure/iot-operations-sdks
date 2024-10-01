// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    internal class FileUtilities
    {
        internal static async Task<byte[]> ReadFileWithRetryAsync(string path, int maxRetryCount = 10, TimeSpan? delayBetweenAttempts = null)
        {
            TimeSpan delay = delayBetweenAttempts ?? TimeSpan.FromMilliseconds(100);

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

                    await Task.Delay(delay);
                }
            }
        }
    }
}
