// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Files.FileMonitor
{
    public class FsnotifyFilesMonitor
    {
        private readonly string _directory;
        private readonly string? _fileName;

        private FileSystemWatcher? _watcher;

        internal event EventHandler<FileChangedEventArgs>? OnFileChanged;

        private bool _startedObserving = false;

        public FsnotifyFilesMonitor(string directory, string? fileName)
        {
            _directory = directory;
            _fileName = fileName;
        }

        public void Start()
        {
            if (_startedObserving)
            {
                return;
            }

            _startedObserving = true;

            _watcher = new FileSystemWatcher(_directory)
            {
                NotifyFilter = NotifyFilters.Attributes
                                     | NotifyFilters.CreationTime
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.FileName
                                     | NotifyFilters.LastAccess
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.Size
            };

            if (_fileName != null)
            {
                // Watch only this file in the directory
                _watcher.Filter = _fileName;
            }

            _watcher.Created += OnChanged;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.IncludeSubdirectories = false;
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.Created -= OnChanged;
                _watcher.Changed -= OnChanged;
                _watcher.Deleted -= OnChanged;
            }

            _watcher?.Dispose();
            _startedObserving = false;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(sender, new(e.FullPath, e.ChangeType));
        }
    }
}
