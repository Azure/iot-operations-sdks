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

        // The set of file paths and their last known state
        private Dictionary<string, FileState> _lastKnownDirectoryState = new();
        
        private string _directoryToObserve;

        private TimeSpan _pollingInterval;

        internal event EventHandler<FileChangedEventArgs>? OnFileChanged;

        private bool _startedObserving = false;

        internal FilesObserver(string directoryToObserve, TimeSpan? pollingInterval = null)
        {
            _directoryToObserve = directoryToObserve;
            _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(10);
        }

        internal async Task StartAsync()
        {
            if (_startedObserving)
            {
                return;
            }

            _startedObserving = true;

            _observationTaskCancellationTokenSource = new();

            foreach (string filePath in Directory.EnumerateFiles(_directoryToObserve))
            {
                await SaveFileStateAsync(filePath);
            }

            var observationTask = new Task(
                async () =>
                {
                    try
                    {
                        while (!_observationTaskCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            var currentFilesInDirectory = Directory.EnumerateFiles(_directoryToObserve);

                            // Check if any previously known files are gone now
                            foreach (string filePath in _lastKnownDirectoryState.Keys)
                            {
                                if (!currentFilesInDirectory.Contains(filePath))
                                {
                                    _lastKnownDirectoryState.Remove(filePath);
                                    OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, ChangeType.Deleted));
                                }
                            }

                            // Check if any previously known files were updated or if any unknown files have been added to this directory
                            foreach (string filePath in currentFilesInDirectory)
                            {
                                try
                                {
                                    //TODO need testing on file create/delete cases
                                    if (!_lastKnownDirectoryState.ContainsKey(filePath))
                                    {
                                        await SaveFileStateAsync(filePath);
                                        OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, ChangeType.Created));
                                    }
                                    else
                                    {
                                        DateTime lastWriteUpdate = File.GetLastWriteTimeUtc(filePath);
                                        if (lastWriteUpdate == _lastKnownDirectoryState[filePath].MostRecentWrite)
                                        {
                                            // File hasn't been updated recently. Skip reading this file's contents.
                                            continue;
                                        }

                                        byte[] contents = await FileUtilities.ReadFileWithRetryAsync(filePath);

                                        byte[] contentsHash = SHA1.HashData(contents);

                                        if (!Enumerable.SequenceEqual(_lastKnownDirectoryState[filePath].MostRecentContentsHash, contentsHash))
                                        {
                                            _lastKnownDirectoryState[filePath] = new(contentsHash, lastWriteUpdate);
                                            OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, ChangeType.Updated));
                                        }
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

        internal async Task SaveFileStateAsync(string filePath)
        {
            _lastKnownDirectoryState.Add(filePath, new(SHA1.HashData(await FileUtilities.ReadFileWithRetryAsync(filePath)), File.GetLastWriteTimeUtc(filePath)));
        }
    }
}
