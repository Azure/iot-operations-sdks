// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.Files.FilesMonitor
{
    /// <summary>
    /// Defines the interface for watching a directory and/or file for creations, updates, and deletions.
    /// </summary>
    /// <see cref="FsnotifyFilesMonitor"/> and <see cref="PollingFilesMonitor"/> for two different implementations.
    public interface IFilesMonitor
    {
        /// <summary>
        /// Event that executes every time the monitored directory and/or file has been created, updated, or deleted with
        /// information about what kind of change it was and what file it was that changed.
        /// </summary>
        event EventHandler<FileChangedEventArgs>? OnFileChanged;

        /// <summary>
        /// Start monitoring the provided directory and/or file.
        /// </summary>
        /// <param name="directory">The directory to monitor.</param>
        /// <param name="file">The specific file to monitor. If not provided, all files in this directory will be monitored.</param>
        void Start(string directory, string? file = null);

        /// <summary>
        /// Stop monitoring the provided directory and/or file
        /// </summary>
        void Stop();
    }
}
