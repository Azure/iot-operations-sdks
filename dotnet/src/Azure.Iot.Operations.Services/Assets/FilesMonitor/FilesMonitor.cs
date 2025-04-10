// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets.FileMonitor
{
    /// <summary>
    /// A utility for monitoring for changes in any of a set of files.
    /// </summary>
    internal class FilesMonitor
    {
        private readonly string _directory;

        private FileSystemWatcher? _directoryWatcher;

        internal event EventHandler<FileChangedEventArgs>? OnFileChanged;

        private bool _startedObserving = false;

        internal FilesMonitor(string directory)
        {
            _directory = directory;
        }

        internal void Start()
        {
            if (_startedObserving)
            {
                return;
            }

            _startedObserving = true;

            _directoryWatcher = new FileSystemWatcher(_directory)
            {
                NotifyFilter = NotifyFilters.Attributes
                                     | NotifyFilters.CreationTime
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.FileName
                                     | NotifyFilters.LastAccess
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.Size
            };

            _directoryWatcher.Created += OnChanged;
            _directoryWatcher.Changed += OnChanged;
            _directoryWatcher.Deleted += OnChanged;
            _directoryWatcher.IncludeSubdirectories = false; //TODO separate watchers for AEP level changes and Asset level changes, right?
            _directoryWatcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(sender, new(e.FullPath, e.ChangeType));
        }

        internal void Stop()
        {
            _directoryWatcher?.Dispose();
            _startedObserving = false;
        }
    }
}
