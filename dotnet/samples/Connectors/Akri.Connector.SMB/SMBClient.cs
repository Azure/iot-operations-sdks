// <copyright file="SMBClient.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMBLibrary;
using SMBLibrary.Client;
using System.Text;

namespace Akri.Connector.SMB;

/// <summary>
/// SMB client wrapper using SMBLibrary for async operations.
/// </summary>
public sealed class SMBClient : ISMBClient, IDisposable
{
    private readonly ILogger<SMBClient> _logger;
    private readonly IOptionsMonitor<SMBConnectorOptions> _options;
    private SMB2Client? _client;
    private ISMBFileStore? _fileStore;
    private bool _isConnected;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public SMBClient(
        ILogger<SMBClient> logger,
        IOptionsMonitor<SMBConnectorOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(
        string host,
        int port,
        string shareName,
        ConnectorAuthentication authentication,
        CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                return;
            }

            var options = _options.CurrentValue;
            _logger.LogInformation("Connecting to SMB server {Host}:{Port}, share {Share}",
                host, port, shareName);

            _client = new SMB2Client();

            // Connect to server using the configured endpoint port.
            if (port != 445)
            {
                _logger.LogWarning("SMBLibrary client does not support custom ports. Using default SMB port 445 instead of {Port}.", port);
            }

            var connectTask = Task.Run(() => _client.Connect(host, SMBTransportType.DirectTCPTransport), cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds), cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Connection to {host}:{port} timed out after {options.ConnectionTimeoutSeconds} seconds");
            }

            if (!await connectTask)
            {
                throw new InvalidOperationException($"Failed to connect to SMB server {host}:{port}");
            }

            var auth = authentication ?? ConnectorAuthentication.Anonymous;
            string username;
            string password;

            switch (auth.Kind)
            {
                case ConnectorAuthenticationKind.Anonymous:
                    username = string.Empty;
                    password = string.Empty;
                    break;
                case ConnectorAuthenticationKind.UsernamePassword:
                    username = auth.Username ?? string.Empty;
                    password = auth.Password ?? string.Empty;
                    break;
                case ConnectorAuthenticationKind.X509:
                    throw new NotSupportedException("X.509 authentication is not supported for SMB connections.");
                default:
                    throw new InvalidOperationException($"Unsupported authentication kind: {auth.Kind}");
            }

            // Log auth kind and whether a username is present (do not log password)
            _logger.LogInformation("Login to SMB server using {AuthKind} authentication (username present: {HasUsername})", auth.Kind, !string.IsNullOrEmpty(username));

            var status = _client.Login(string.Empty, username, password);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new InvalidOperationException($"SMB login failed with status: {status}");
            }

            _logger.LogDebug("SMB authentication successful");

            // Connect to share
            _fileStore = _client.TreeConnect(shareName, out status);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new InvalidOperationException($"Failed to connect to share {shareName}: {status}");
            }

            _isConnected = true;
            _logger.LogInformation("Successfully connected to SMB share {Share}", shareName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to SMB share");
            _client?.Disconnect();
            _client = null;
            _fileStore = null;
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isConnected)
            {
                return;
            }

            _logger.LogDebug("Disconnecting from SMB share");
            _fileStore?.Disconnect();
            _fileStore = null;

            _client?.Disconnect();
            _client = null;
            _isConnected = false;

            _logger.LogInformation("Disconnected from SMB share");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<FileMetadata>> ListFilesAsync(string path, string pattern, CancellationToken cancellationToken)
    {
        if (!_isConnected || _fileStore == null)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        return await Task.Run(() =>
        {
            var files = new List<FileMetadata>();
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(pattern);

            var normalizedPath = NormalizeShareRelativePath(path);
            var openPath = string.IsNullOrEmpty(normalizedPath) ? string.Empty : normalizedPath;
            var logPath = string.IsNullOrEmpty(normalizedPath) ? "<root>" : normalizedPath;

            _logger.LogDebug("Listing files in {Path} with pattern {Pattern}", logPath, pattern);

            try
            {
                // Query directory
                var status = _fileStore.CreateFile(
                    out object? directoryHandle,
                    out FileStatus fileStatus,
                    openPath,
                    AccessMask.GENERIC_READ,
                    0,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    _logger.LogWarning("Failed to open directory {Path}: {Status}", logPath, status);
                    return files;
                }

                try
                {
                    // Query directory contents
                    status = _fileStore.QueryDirectory(
                        out var fileList,
                        directoryHandle,
                        "*",
                        FileInformationClass.FileDirectoryInformation);

                    if (status != NTStatus.STATUS_SUCCESS)
                    {
                        _logger.LogWarning("Failed to query directory {Path}: {Status}", logPath, status);
                        return files;
                    }

                    if (fileList == null)
                    {
                        return files;
                    }

                    // Filter and convert to FileMetadata
                    foreach (var fileInfo in fileList)
                    {
                        if (fileInfo is not FileDirectoryInformation dirInfo)
                        {
                            continue;
                        }

                        _logger.LogTrace("Processing file entry {FileName} (attributes: {Attributes})", dirInfo.FileName, dirInfo.FileAttributes);

                        // Skip directories and special entries
                        if ((dirInfo.FileAttributes & SMBLibrary.FileAttributes.Directory) != 0 ||
                            dirInfo.FileName == "." ||
                            dirInfo.FileName == "..")
                        {
                            continue;
                        }

                        // Match against pattern
                        if (!matcher.Match(dirInfo.FileName).HasMatches)
                        {
                            continue;
                        }

                        var filePath = string.IsNullOrEmpty(normalizedPath)
                            ? dirInfo.FileName
                            : normalizedPath + "\\" + dirInfo.FileName;

                        files.Add(new FileMetadata
                        {
                            Path = filePath,
                            Size = dirInfo.EndOfFile,
                            LastModified = System.DateTime.FromFileTimeUtc(dirInfo.LastWriteTime.ToFileTime()),
                        });
                    }

                    _logger.LogDebug("Found {Count} files matching pattern {Pattern}", files.Count, pattern);
                }
                finally
                {
                    _fileStore.CloseFile(directoryHandle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files in {Path}", logPath);
                throw;
            }

            return files;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!_isConnected || _fileStore == null)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        return await Task.Run(() =>
        {
            var normalizedPath = NormalizeShareRelativePath(path);
            var openPath = string.IsNullOrEmpty(normalizedPath) ? "\\" : normalizedPath;

            _logger.LogDebug("Reading file {Path}", openPath);

            try
            {
                // Open file
                var status = _fileStore.CreateFile(
                    out object? fileHandle,
                    out FileStatus fileStatus,
                    openPath,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    0,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    throw new InvalidOperationException($"Failed to open file {openPath}: {status}");
                }

                try
                {
                    // Get file size
                    status = _fileStore.GetFileInformation(out FileInformation? fileInfo, fileHandle, FileInformationClass.FileStandardInformation);
                    if (status != NTStatus.STATUS_SUCCESS)
                    {
                        throw new InvalidOperationException($"Failed to get file information for {openPath}: {status}");
                    }

                    var fileSize = ((FileStandardInformation)fileInfo!).EndOfFile;
                    if (fileSize == 0)
                    {
                        return string.Empty;
                    }

                    // Read file content
                    var buffer = new byte[fileSize];
                    long bytesRead = 0;
                    const int maxReadSize = 65536; // 64KB chunks

                    while (bytesRead < fileSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var readSize = (int)Math.Min(maxReadSize, fileSize - bytesRead);
                        status = _fileStore.ReadFile(out byte[]? data, fileHandle, bytesRead, readSize);

                        if (status != NTStatus.STATUS_SUCCESS)
                        {
                            throw new InvalidOperationException($"Failed to read from file {openPath} at offset {bytesRead}: {status}");
                        }

                        if (data == null || data.Length == 0)
                        {
                            break;
                        }

                        Array.Copy(data, 0, buffer, bytesRead, data.Length);
                        bytesRead += data.Length;
                    }

                    var content = Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                    _logger.LogDebug("Read {Bytes} bytes from file {Path}", bytesRead, openPath);
                    return content;
                }
                finally
                {
                    _fileStore.CloseFile(fileHandle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file {Path}", openPath);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken)
    {
        if (!_isConnected || _fileStore == null)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        return await Task.Run(() =>
        {
            var normalizedPath = NormalizeShareRelativePath(path);
            var openPath = string.IsNullOrEmpty(normalizedPath) ? string.Empty : normalizedPath;

            try
            {
                var status = _fileStore.CreateFile(
                    out object? directoryHandle,
                    out FileStatus fileStatus,
                    openPath,
                    AccessMask.GENERIC_READ,
                    0,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    _fileStore.CloseFile(directoryHandle);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<List<string>> ListDirectoriesAsync(string path, CancellationToken cancellationToken)
    {
        if (!_isConnected || _fileStore == null)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        return await Task.Run(() =>
        {
            var directories = new List<string>();

            var normalizedPath = NormalizeShareRelativePath(path);
            var openPath = string.IsNullOrEmpty(normalizedPath) ? string.Empty : normalizedPath;
            var logPath = string.IsNullOrEmpty(normalizedPath) ? "<root>" : normalizedPath;

            _logger.LogDebug("Listing directories in {Path}", logPath);

            try
            {
                // Query directory
                var status = _fileStore.CreateFile(
                    out object? directoryHandle,
                    out FileStatus fileStatus,
                    openPath,
                    AccessMask.GENERIC_READ,
                    0,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    _logger.LogWarning("Failed to open directory {Path}: {Status}", logPath, status);
                    return directories;
                }

                try
                {
                    // Query directory contents
                    status = _fileStore.QueryDirectory(
                        out List<QueryDirectoryFileInformation>? fileList,
                        directoryHandle,
                        "*",
                        FileInformationClass.FileDirectoryInformation);

                    if (status != NTStatus.STATUS_SUCCESS)
                    {
                        _logger.LogDebug("Query directory completed for {Path} with status {Status}", logPath, status);
                        return directories;
                    }

                    if (fileList == null)
                    {
                        return directories;
                    }

                    // Filter for directories only
                    foreach (var fileInfo in fileList)
                    {
                        if (fileInfo is not FileDirectoryInformation dirInfo)
                        {
                            continue;
                        }

                        // Skip files and special entries
                        if ((dirInfo.FileAttributes & SMBLibrary.FileAttributes.Directory) == 0 ||
                            dirInfo.FileName == "." ||
                            dirInfo.FileName == "..")
                        {
                            continue;
                        }

                        var directoryPath = string.IsNullOrEmpty(normalizedPath)
                            ? dirInfo.FileName
                            : normalizedPath + "\\" + dirInfo.FileName;

                        directories.Add(directoryPath);
                    }

                    _logger.LogDebug("Found {Count} directories", directories.Count);
                }
                finally
                {
                    _fileStore.CloseFile(directoryHandle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing directories in {Path}", logPath);
                throw;
            }

            return directories;
        }, cancellationToken);
    }

    private static string NormalizeShareRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('/', '\\').Trim('\\');
    }


    public void Dispose()
    {
        try
        {
            DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore disposal errors
        }

        _connectionLock.Dispose();
    }
}
