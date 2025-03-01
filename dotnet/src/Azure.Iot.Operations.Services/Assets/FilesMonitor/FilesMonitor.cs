// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Services.Assets.FileMonitor
{
    /// <summary>
    /// A utility for monitoring for changes in any of a set of files.
    /// </summary>
    internal class FilesMonitor
    {
        private CancellationTokenSource? _observationTaskCancellationTokenSource;

        // The set of file paths and their last known contents hash
        private readonly Dictionary<string, byte[]> _lastKnownDirectoryState = new();

        private readonly Func<string> _directoryRetriever;

        private readonly TimeSpan _pollingInterval;

        internal event EventHandler<FileChangedEventArgs>? OnFileChanged;

        private bool _startedObserving = false;

        internal FilesMonitor(Func<string> directoryRetriever, TimeSpan? pollingInterval = null)
        {
            _directoryRetriever = directoryRetriever;
            _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(10);
        }

        internal void Start()
        {
            if (_startedObserving)
            {
                return;
            }

            _startedObserving = true;

            _observationTaskCancellationTokenSource = new();

            var observationTask = new Task(
                async () =>
                {
                    try
                    {
                        while (!_observationTaskCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            string directoryToObserve = _directoryRetriever.Invoke();
                            if (string.IsNullOrWhiteSpace(directoryToObserve) || !Directory.Exists(directoryToObserve))
                            {
                                // The folder was deleted, so all previously known files must have been deleted as well
                                foreach (string filePath in _lastKnownDirectoryState.Keys)
                                {
                                    OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, ChangeType.Deleted));
                                }

                                _lastKnownDirectoryState.Clear();
                            }
                            else
                            {
                                var currentFilesInDirectory = Directory.EnumerateFiles(directoryToObserve);

                                // Check if any previously known files are gone now
                                List<string> filePathsToRemove = new();
                                foreach (string filePath in _lastKnownDirectoryState.Keys)
                                {
                                    if (!currentFilesInDirectory.Contains(filePath))
                                    {
                                        filePathsToRemove.Add(filePath);
                                    }
                                }

                                foreach (string filePathToRemove in filePathsToRemove)
                                {
                                    _lastKnownDirectoryState.Remove(filePathToRemove);
                                    OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePathToRemove, ChangeType.Deleted));
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
                                            byte[] contents = await FileUtilities.ReadFileWithRetryAsync(filePath);

                                            byte[] contentsHash = SHA1.HashData(contents);

                                            if (!_lastKnownDirectoryState[filePath].SequenceEqual(contentsHash))
                                            {
                                                _lastKnownDirectoryState[filePath] = contentsHash;
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
                    }
                    catch (ObjectDisposedException)
                    {
                        // The cancellation token used to control this thread has been disposed. End this thread gracefully
                    }
                    finally
                    {
                        _startedObserving = false;
                    }
                });

            observationTask.Start();
        }

        internal void Stop()
        {
            _observationTaskCancellationTokenSource?.Cancel();
            _observationTaskCancellationTokenSource?.Dispose();
        }

        internal async Task SaveFileStateAsync(string filePath)
        {
            _lastKnownDirectoryState.Add(filePath, SHA1.HashData(await FileUtilities.ReadFileWithRetryAsync(filePath)));
        }
    }
}
