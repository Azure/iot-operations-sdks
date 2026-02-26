// <copyright file="ISMBClient.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.Connector.SMB;

/// <summary>
/// Interface for SMB client operations.
/// </summary>
public interface ISMBClient
{
    /// <summary>
    /// Connects to the SMB share on the specified server.
    /// </summary>
    /// <param name="host">The SMB server hostname or IP address.</param>
    /// <param name="port">The SMB server port (typically 445).</param>
    /// <param name="shareName">The SMB share name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(
        string host,
        int port,
        string shareName,
        Akri.HistorianConnector.Core.Models.ConnectorAuthentication authentication,
        CancellationToken cancellationToken);

    /// <summary>
    /// Disconnects from the SMB share.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists files in the specified directory matching the given pattern.
    /// </summary>
    Task<List<FileMetadata>> ListFilesAsync(string path, string pattern, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the content of a file.
    /// </summary>
    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a list of directory paths from the specified location.
    /// </summary>
    /// <remarks>This method may throw an exception if the specified path is invalid or if there are
    /// insufficient permissions to access the directory.</remarks>
    /// <param name="path">The path to the directory from which to list subdirectories. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of strings that represent the paths of the
    /// directories found. The list will be empty if no directories are found.</returns>
    Task<List<string>> ListDirectoriesAsync(string path, CancellationToken cancellationToken);
}

/// <summary>
/// Metadata for a file in the SMB share.
/// </summary>
public sealed class FileMetadata
{
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; set; }
}
