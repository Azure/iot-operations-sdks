// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    /// <summary>
    /// EventArgs that contains context on what change happened to which file
    /// </summary>
    public class FileChangedEventArgs : EventArgs
    {
        public WatcherChangeTypes ChangeType { get; init; }

        public string FilePath { get; init; }

        public string FileName
        {
            get
            {
                return Path.GetFileName(FilePath);
            }
        }

        public FileChangedEventArgs(string filePath, WatcherChangeTypes changeType)
        {
            FilePath = filePath;
            ChangeType = changeType;
        }
    }
}
