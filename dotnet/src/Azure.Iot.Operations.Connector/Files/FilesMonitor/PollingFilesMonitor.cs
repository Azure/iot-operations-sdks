// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    /// <summary>
    /// An implementation of <see cref="IFilesMonitor"/> that relies on the polling the state of the file system rather than
    /// listening for notifications from the operating system.
    /// </summary>
    public class PollingFilesMonitor : IFilesMonitor
    {
        private CancellationTokenSource? _observationTaskCancellationTokenSource;

        // The set of file paths and their last known contents hash
        private readonly Dictionary<string, byte[]> _lastKnownDirectoryState = new();

        private readonly TimeSpan _pollingInterval;

        private bool _startedObserving = false;

        public event EventHandler<FileChangedEventArgs>? OnFileChanged;

        public PollingFilesMonitor(TimeSpan? pollingInterval = null)
        {
            _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(10);
        }

        public void Start(string directory, string? fileName = null)
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
                            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                            {
                                // The folder was deleted, so all previously known files must have been deleted as well
                                foreach (string filePath in _lastKnownDirectoryState.Keys)
                                {
                                    if (fileName == null || filePath.Contains(fileName))
                                    {
                                        OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, WatcherChangeTypes.Deleted));
                                    }
                                }

                                _lastKnownDirectoryState.Clear();
                            }
                            else
                            {
                                var currentFilesInDirectory = Directory.EnumerateFiles(directory);

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
                                    if (fileName == null || filePathsToRemove.Contains(fileName))
                                    {
                                        OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePathToRemove, WatcherChangeTypes.Deleted));
                                    }
                                }

                                // Check if any previously known files were updated or if any unknown files have been added to this directory
                                foreach (string filePath in currentFilesInDirectory)
                                {
                                    try
                                    {
                                        if (!_lastKnownDirectoryState.ContainsKey(filePath))
                                        {
                                            SaveFileState(filePath);
                                            OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, WatcherChangeTypes.Created));
                                        }
                                        else
                                        {
                                            byte[] contents = FileUtilities.ReadFileWithRetry(filePath);

                                            byte[] contentsHash = SHA1.HashData(contents);

                                            if (!_lastKnownDirectoryState[filePath].SequenceEqual(contentsHash))
                                            {
                                                _lastKnownDirectoryState[filePath] = contentsHash;
                                                if (fileName == null || filePath.Contains(fileName))
                                                {
                                                    OnFileChanged?.Invoke(this, new FileChangedEventArgs(filePath, WatcherChangeTypes.Changed));
                                                }
                                            }
                                        }
                                    }
                                    catch (IOException e)
                                    {
                                        // File may have been accessed by another process. Ignore error and try again.
                                        Trace.TraceWarning("Failed to access file with path {0} due to error {1}", filePath, e);
                                    }
                                }
                            }

                            await Task.Delay(_pollingInterval);
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

        public void Stop()
        {
            _observationTaskCancellationTokenSource?.Cancel();
            _observationTaskCancellationTokenSource?.Dispose();
        }

        internal void SaveFileState(string filePath)
        {
            _lastKnownDirectoryState.Add(filePath, SHA1.HashData(FileUtilities.ReadFileWithRetry(filePath)));
        }
    }
}
