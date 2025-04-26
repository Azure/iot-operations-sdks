// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets.FileMonitor
{
    /// <summary>
    /// EventArgs that contains context on what change happened to which file
    /// </summary>
    internal class FileChangedEventArgs : EventArgs
    {
        internal WatcherChangeTypes ChangeType { get; init; }

        internal string FilePath { get; init; }

        internal string FileName
        {
            get
            {
                return Path.GetFileName(FilePath);
            }
        }

        internal FileChangedEventArgs(string filePath, WatcherChangeTypes changeType)
        {
            FilePath = filePath;
            ChangeType = changeType;
        }
    }
}
