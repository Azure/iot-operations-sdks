// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    internal class FilesObserver
    {
        private CancellationTokenSource? _observationTaskCancellationTokenSource;

        private Dictionary<string, DateTime> _mostRecentWriteUpdate = new();
        private Dictionary<string, byte[]> _mostRecentContentsHash = new();
        private List<string> _filePathsToObserve;
        private TimeSpan _pollingInterval;

        internal event EventHandler? OnFileChanged;

        internal FilesObserver(List<string> filePathsToObserve, TimeSpan? pollingInterval = null)
        {
            _filePathsToObserve = filePathsToObserve;
            _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(10);
        }

        internal void Start()
        {
            //TODO re-entrancy?
            _observationTaskCancellationTokenSource = new();

            foreach (string filePath in _filePathsToObserve)
            {
                _mostRecentContentsHash.Add(filePath, SHA1.HashData(FileUtilities.ReadFileWithRetry(filePath)));
                _mostRecentWriteUpdate.Add(filePath, File.GetLastWriteTimeUtc(filePath));
            }

            // TODO monitoring lastWrite attribute hit the same problem as FileSystemWatcher in that it is updated
            // prior to the contents actually changing :/
            // Reading the file contents is more expensive and risks race conditions/IOExceptions
            var observationTask =
                new Task(
                async () =>
                {
                    while (!_observationTaskCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        foreach (string filePath in _filePathsToObserve)
                        {
                            try
                            {
                                DateTime lastWriteUpdate = File.GetLastWriteTimeUtc(filePath);
                                if (lastWriteUpdate == _mostRecentWriteUpdate[filePath])
                                {
                                    // File hasn't been updated recently. Skip reading this file's contents.
                                    continue;
                                }

                                byte[] contents = FileUtilities.ReadFileWithRetry(filePath);

                                byte[] contentsHash = SHA1.HashData(contents);

                                if (!Enumerable.SequenceEqual(_mostRecentContentsHash[filePath], contentsHash))
                                {
                                    _mostRecentContentsHash[filePath] = contentsHash;
                                    _mostRecentWriteUpdate[filePath] = lastWriteUpdate;
                                    OnFileChanged?.Invoke(this, new());
                                }
                            }
                            catch (IOException e)
                            {
                                // File may have been accessed by another process. Ignore error and try again.
                                Trace.TraceWarning("Failed to access file with path {0} due to error {1}", filePath, e);
                            }
                        }
                            
                        await Task.Delay(_pollingInterval);
                    }
                });

            observationTask.Start();
        }

        internal void Stop()
        {
            _observationTaskCancellationTokenSource?.Cancel();
        }
    }
}
