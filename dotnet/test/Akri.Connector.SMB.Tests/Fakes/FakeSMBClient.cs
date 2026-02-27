// <copyright file="FakeSMBClient.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;

namespace Akri.Connector.SMB.Tests.Fakes;

/// <summary>
/// Fake SMB client for testing.
/// </summary>
public sealed class FakeSMBClient : ISMBClient
{
    private bool _isConnected;
    private readonly Dictionary<string, FileMetadata> _files = new();
    private readonly HashSet<string> _directories = new();

    public bool ConnectCalled { get; private set; }
    public bool DisconnectCalled { get; private set; }

    public FakeSMBClient()
    {
        // Add default directories
        _directories.Add("/");
        _directories.Add("/data");
    }

    /// <summary>
    /// Adds a file to the fake SMB share.
    /// </summary>
    public void AddFile(string path, long size, DateTimeOffset lastModified)
    {
        _files[path] = new FileMetadata
        {
            Path = path,
            Size = size,
            LastModified = lastModified
        };
    }

    /// <summary>
    /// Adds a directory to the fake SMB share.
    /// </summary>
    public void AddDirectory(string path)
    {
        _directories.Add(path);
    }

    public string? LastConnectedHost { get; private set; }
    public int LastConnectedPort { get; private set; }
    public string? LastConnectedShare { get; private set; }

    public Task ConnectAsync(
        string host,
        int port,
        string shareName,
        ConnectorAuthentication authentication,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectCalled = true;
        _isConnected = true;
        LastConnectedHost = host;
        LastConnectedPort = port;
        LastConnectedShare = shareName;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        DisconnectCalled = true;
        cancellationToken.ThrowIfCancellationRequested();
        _isConnected = false;
        return Task.CompletedTask;
    }

    public Task<List<FileMetadata>> ListFilesAsync(string path, string pattern, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        var matchingFiles = _files.Values
            .Where(f => f.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                var relativePath = f.Path.Substring(path.Length).TrimStart('/');
                // Simple pattern matching for testing - convert glob to regex
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(
                    relativePath.Replace('\\', '/'),
                    regexPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            })
            .ToList();

        return Task.FromResult(matchingFiles);
    }

    public Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        if (!_files.ContainsKey(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        // Return sample CSV content with timestamps relative to now
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(
            "timestamp,tag,value,quality\n" +
            $"{now.AddHours(-2):o},tag1,25.5,0\n" +
            $"{now.AddHours(-1):o},tag2,30.2,0\n");
    }

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        // Handle empty string or "/" as root directory
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return Task.FromResult(_directories.Contains("/"));
        }

        return Task.FromResult(_directories.Contains(path));
    }

    public Task<List<string>> ListDirectoriesAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to SMB share");
        }

        var normalizedBase = string.IsNullOrEmpty(path) ? "/" : path.TrimEnd('/');

        var directories = _directories
            .Where(directory =>
                normalizedBase == "/"
                    ? directory != "/"
                    : directory.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(directories);
    }
}
