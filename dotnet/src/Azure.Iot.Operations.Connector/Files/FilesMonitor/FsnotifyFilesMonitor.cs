// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    /// <summary>
    /// An implementation of <see cref="IFilesMonitor"/> that relies on the operating system's Fsnotify.
    /// </summary>
    public class FsnotifyFilesMonitor : IFilesMonitor
    {
        private FileSystemWatcher? _watcher;

        private bool _startedObserving = false;

        public event EventHandler<FileChangedEventArgs>? OnFileChanged;

        public void Start(string directory, string? fileName = null)
        {
            if (_startedObserving)
            {
                return;
            }

            _startedObserving = true;

            _watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.Attributes
                                     | NotifyFilters.CreationTime
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.FileName
                                     | NotifyFilters.LastAccess
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.Size
            };

            if (fileName != null)
            {
                // Watch only this file in the directory
                _watcher.Filter = fileName;
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
