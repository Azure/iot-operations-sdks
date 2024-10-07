// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    /// <summary>
    /// A utility for monitoring for changes in any of a set of files.
    /// </summary>
    internal class FilesObserver
    {
        private CancellationTokenSource? _observationTaskCancellationTokenSource;

        // The most recent write update for each file
        private Dictionary<string, DateTime> _mostRecentWriteUpdate = new();
        
        // The most recent hash of the contents of each file
        private Dictionary<string, byte[]> _mostRecentContentsHash = new();
        
        // The list of files to observe. They may be in different directories.
        private List<string> _filePathsToObserve;

        private TimeSpan _pollingInterval;

        internal event EventHandler? OnFileChanged;

        private bool _startedObserving = false;

        internal FilesObserver(List<string> filePathsToObserve, TimeSpan? pollingInterval = null)
        {
            _filePathsToObserve = filePathsToObserve;
            _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(10);
        }

        internal void ObserveAdditionalFilePath(string filePathToObserve)
        {
            _filePathsToObserve.Add(filePathToObserve);
        }

        internal async Task StartAsync()
        {
            if (_startedObserving)
            {
                return;
            }

            _startedObserving = true;

            _observationTaskCancellationTokenSource = new();

            foreach (string filePath in _filePathsToObserve)
            {
                _mostRecentContentsHash.Add(filePath, SHA1.HashData(await FileUtilities.ReadFileWithRetryAsync(filePath)));
                _mostRecentWriteUpdate.Add(filePath, File.GetLastWriteTimeUtc(filePath));
            }

            var observationTask = new Task(
                async () =>
                {
                    try
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

                                    byte[] contents = await FileUtilities.ReadFileWithRetryAsync(filePath);

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
                    }
                    finally
                    {
                        _startedObserving = false;
                    }
                });

            observationTask.Start();
        }

        internal Task StopAsync()
        {
            _observationTaskCancellationTokenSource?.Cancel();
            return Task.CompletedTask;
        }
    }
}
