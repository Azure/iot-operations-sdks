// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets.FileMonitor
{
    /// <summary>
    /// EventArgs that contains context on what change happened to which file
    /// </summary>
    internal class FileChangedEventArgs : EventArgs
    {
        internal ChangeType ChangeType { get; init; }

        internal string FilePath { get; init; }

        internal string FileName
        {
            get
            {
                return Path.GetFileName(FilePath);
            }
        }

        internal FileChangedEventArgs(string filePath, ChangeType changeType)
        {
            FilePath = filePath;
            ChangeType = changeType;
        }
    }
}
