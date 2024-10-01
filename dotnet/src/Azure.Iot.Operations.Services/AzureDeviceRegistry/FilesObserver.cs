// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Caching;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    internal class FilesObserver
    {
        private CancellationTokenSource? _observationTaskCancellationTokenSource;
        private Dictionary<string, byte[]> mostRecentContents = new();
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
                byte[] contents = File.ReadAllBytes(filePath);

                mostRecentContents.Add(filePath, contents);
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

                                if (!Enumerable.SequenceEqual(mostRecentContents[filePath], contents))
                                {
                                    mostRecentContents[filePath] = contents;

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
