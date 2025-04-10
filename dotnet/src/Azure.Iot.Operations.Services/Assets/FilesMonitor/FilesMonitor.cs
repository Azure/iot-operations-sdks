// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

            _directoryWatcher.Created += OnCreated;
            _directoryWatcher.Deleted += OnDeleted;
            _directoryWatcher.IncludeSubdirectories = true;
            _directoryWatcher.EnableRaisingEvents = true;
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(sender, new(e.FullPath, ChangeType.Deleted));
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(sender, new(e.FullPath, ChangeType.Created));
        }

        internal void Stop()
        {
        }
    }
}
