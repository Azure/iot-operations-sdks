// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Caching;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    internal class FilesObserver
    {
        private CancellationTokenSource? _observationTaskCancellationTokenSource;

        private Dictionary<string, byte[]> mostRecentContentsHash = new();
        private List<string> _filePathsToObserve;

        internal event EventHandler? OnFileChanged;

        internal FilesObserver(List<string> filePathsToObserve)
        {
            _filePathsToObserve = filePathsToObserve;
        }

        internal void Start()
        {
            //TODO re-entrancy?
            _observationTaskCancellationTokenSource = new();

            foreach (string filePath in _filePathsToObserve)
            {
                //TODO retry
                mostRecentContentsHash.Add(filePath, SHA1.HashData(File.ReadAllBytes(filePath)));
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
                        try
                        {
                            foreach (string filePath in _filePathsToObserve)
                            {
                                byte[] contents = File.ReadAllBytes(filePath);

                                byte[] contentsHash = SHA1.HashData(contents);

                                if (!Enumerable.SequenceEqual(mostRecentContentsHash[filePath], contentsHash))
                                {
                                    mostRecentContentsHash[filePath] = contentsHash;

                                    OnFileChanged?.Invoke(this, new());
                                }
                            }

                            //TODO configurable
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                        catch (IOException)
                        {
                            // File may have been accessed by another process. Ignore error and try again.
                        }
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
