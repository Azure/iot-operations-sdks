// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets
{
    internal class FileUtilities
    {
        // There is some risk that the AssetMonitor will try to read a file while it is being written to
        // by the Akri operator, so this utility function provides some basic retry logic to mitigate that risk.
        internal static byte[] ReadFileWithRetry(string path, int maxRetryCount = 10, TimeSpan? delayBetweenAttempts = null)
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
                    if (retryCount > maxRetryCount)
                    {
                        throw;
                    }

                    Task.Delay(delay).RunSynchronously();
                }
            }
        }

        internal static IEnumerable<string> ReadFileLinesWithRetry(string path, int maxRetryCount = 10, TimeSpan? delayBetweenAttempts = null)
        {
            TimeSpan delay = delayBetweenAttempts ?? TimeSpan.FromMilliseconds(100);

            int retryCount = 0;
            while (true)
            {
                retryCount++;

                try
                {
                    return File.ReadLines(path);
                }
                catch (IOException)
                {
                    if (retryCount > maxRetryCount)
                    {
                        throw;
                    }

                    Task.Delay(delay).RunSynchronously();
                }
            }
        }
    }
}
