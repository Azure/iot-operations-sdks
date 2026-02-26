// <copyright file="SMBHistorianExecutor.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.Connector.SMB.Models;
using Akri.HistorianConnector.Core.Contracts;
using Akri.HistorianConnector.Core.Models;
using Akri.HistorianConnector.Core.StateStore;
using Azure.Iot.Operations.Services.LeaderElection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Akri.Connector.SMB;

/// <summary>
/// SMB historian executor for retrieving time-series data from SMB file shares.
/// </summary>
public sealed class SMBHistorianExecutor : IHistorianQueryExecutor
{
    private readonly ILogger<SMBHistorianExecutor> _logger;
    private readonly IOptionsMonitor<SMBConnectorOptions> _options;
    private readonly ISMBClient _smbClient;
    private readonly IWatermarkStore<WatermarkData> _watermarkStore;
    private readonly LeaderElectionClient? _leaderElectionClient;
    private readonly ConcurrentDictionary<string, byte> _emptyListingDiagnosticsLogged = new();

    public SMBHistorianExecutor(
        ILogger<SMBHistorianExecutor> logger,
        IOptionsMonitor<SMBConnectorOptions> options,
        ISMBClient smbClient,
        IWatermarkStore<WatermarkData> watermarkStore,
        LeaderElectionClient? leaderElectionClient = null)
    {
        _logger = logger;
        _options = options;
        _smbClient = smbClient;
        _watermarkStore = watermarkStore;
        _leaderElectionClient = leaderElectionClient;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HistorianSample> ExecuteAsync(
        HistorianQueryDefinition query,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing SMB historian query {QueryId} for window [{Start:o}, {End:o}] with {DataPointCount} data points",
            query.QueryId,
            windowStart,
            windowEnd,
            query.DataPoints.Count);

        var options = _options.CurrentValue;

        // Check leader election if enabled
        if (options.EnableLeaderElection && _leaderElectionClient != null)
        {
            // Check if we're the current leader by inspecting the last campaign result
            var lastResult = _leaderElectionClient.LastKnownCampaignResult;
            if (lastResult == null || !lastResult.IsLeader)
            {
                _logger.LogInformation("Not the leader, skipping query execution");
                yield break;
            }
        }

        var taskType = string.IsNullOrWhiteSpace(query.TaskType) ? options.TaskType : query.TaskType;

        // T021: Route execution based on task type
        if (string.Equals(taskType, "Copy", StringComparison.OrdinalIgnoreCase))
        {
            // Execute copy task - no samples to yield for copy operations
            await ExecuteCopyTaskAsync(query, windowStart, windowEnd, cancellationToken);
            yield break;
        }

        // Default: Execute parse task (User Story 3)
        // Get watermark for incremental processing
        var watermarkData = await _watermarkStore.GetAsync($"watermark:{query.QueryId}");
        var watermark = watermarkData?.Watermark ?? windowStart;
        _logger.LogDebug("Current watermark for query {QueryId}: {Watermark:o}", query.QueryId, watermark);

        DateTimeOffset? maxTimestamp = watermark;

        try
        {
            // Connect to SMB share using per-query connection details
            if (query.Authentication == ConnectorAuthentication.Anonymous)
            {
                _logger.LogWarning("No authentication provided for query {QueryId}; connecting using Anonymous authentication. Check inbound endpoint settings.", query.QueryId);
            }
            await _smbClient.ConnectAsync(query.Host, query.Port, query.ShareName, query.Authentication, cancellationToken);

            // Build file path pattern
            var basePath = query.BasePath.TrimEnd('/', '\\');
            var fullPath = $"{basePath}";

            // List files matching the pattern
            var listPattern = query.FilePattern;
            // For copy tasks, default to copying all files if pattern is the CSV default
            if (string.Equals(taskType, "Copy", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(listPattern) || listPattern == HistorianAssetToQueryMapper.DefaultFilePattern)
                {
                    listPattern = "*.*";
                }
            }

            var files = await _smbClient.ListFilesAsync(
                fullPath,
                listPattern,
                cancellationToken);

            _logger.LogInformation("Found {FileCount} files matching pattern {Pattern}", files.Count, query.FilePattern);

            if (files.Count == 0 && _emptyListingDiagnosticsLogged.TryAdd(query.QueryId, 0))
            {
                await LogEmptyListingDiagnosticsAsync(query, basePath, listPattern, cancellationToken);
            }

            // Filter files by modification time (only process files modified after watermark)
            var filesToProcess = files
                .Where(f => f.LastModified > watermark)
                .OrderBy(f => f.LastModified)
                .ToList();

            _logger.LogInformation("Processing {FileCount} files modified after watermark", filesToProcess.Count);

            // Process each file
            foreach (var file in filesToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip files that are too large
                if (file.Size > options.MaxFileSizeBytes)
                {
                    _logger.LogWarning(
                        "Skipping file {Path} (size: {Size} bytes) - exceeds max size {MaxSize} bytes",
                        file.Path,
                        file.Size,
                        options.MaxFileSizeBytes);
                    continue;
                }

                _logger.LogDebug("Processing file {Path} (size: {Size} bytes, modified: {Modified:o})",
                    file.Path,
                    file.Size,
                    file.LastModified);

                // Read file content
                string content;
                try
                {
                    content = await _smbClient.ReadFileAsync(file.Path, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading file {Path}, skipping", file.Path);
                    continue;
                }

                // Parse CSV content
                var samples = ParseCsvContent(content, query, windowStart, windowEnd);

                // Advance file-based watermark when the file is successfully read and parsed,
                // even when no rows are yielded for the current window.
                if (!maxTimestamp.HasValue || file.LastModified > maxTimestamp.Value)
                {
                    maxTimestamp = file.LastModified;
                }

                // Yield samples
                foreach (var sample in samples)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return sample;
                }

                _logger.LogDebug("Completed processing file {Path}", file.Path);
            }

            // Update watermark to the maximum file modification timestamp processed
            if (maxTimestamp.HasValue && maxTimestamp.Value > watermark)
            {
                var newWatermarkData = new WatermarkData
                {
                    QueryId = query.QueryId,
                    Watermark = maxTimestamp.Value,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _watermarkStore.SetAsync($"watermark:{query.QueryId}", newWatermarkData);
                _logger.LogInformation("Updated watermark for query {QueryId} to {Watermark:o}",
                    query.QueryId, maxTimestamp.Value);
            }
        }
        finally
        {
            await _smbClient.DisconnectAsync(cancellationToken);
        }

        _logger.LogDebug(
            "Completed SMB historian query {QueryId} for window [{Start:o}, {End:o})",
            query.QueryId,
            windowStart,
            windowEnd);
    }

    /// <inheritdoc />
    public async Task<HistorianValidationResult> ValidateAsync(
        HistorianQueryDefinition query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Validating SMB historian query {QueryId}, host:{Host}, port:{Port}, share:{Share} with {DataPointCount} data points...",
            query.QueryId,
            query.Host,
            query.Port,
            query.ShareName,
            query.DataPoints.Count);

        try
        {
            // Test SMB connection using per-query connection details
            await _smbClient.ConnectAsync(query.Host, query.Port, query.ShareName, query.Authentication, cancellationToken);


            var basePath = query.BasePath.TrimEnd('/', '\\');

            // Verify base path exists
            var exists = await _smbClient.DirectoryExistsAsync(basePath, cancellationToken);
            if (!exists)
            {
                await _smbClient.DisconnectAsync(cancellationToken);
                return HistorianValidationResult.Failure($"Base path '{basePath}' does not exist on SMB share");
            }

            await _smbClient.DisconnectAsync(cancellationToken);

            _logger.LogInformation("SMB connection validated successfully");
            return HistorianValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMB connection validation failed");
            return HistorianValidationResult.Failure($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes Copy task: sync files from SMB to local Kubernetes PV storage (T022, T023).
    /// </summary>
    private async Task ExecuteCopyTaskAsync(
        HistorianQueryDefinition query,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;

        // Get watermark for incremental processing
        var watermarkData = await _watermarkStore.GetAsync($"watermark:{query.QueryId}");
        var watermark = watermarkData?.Watermark ?? windowStart;
        _logger.LogDebug("Current watermark for Copy query {QueryId}: {Watermark:o}", query.QueryId, watermark);

        DateTimeOffset? maxTimestamp = watermark;

        // Track copy statistics
        var filesCopied = 0;
        var filesSkipped = 0;
        var filesFailed = 0;
        var totalBytesCopied = 0L;

        // Track files for deletion detection
        var previousFiles = new HashSet<string>();
        var currentFiles = new HashSet<string>();

        try
        {
            // Connect to SMB share using per-query connection details
            if (query.Authentication == ConnectorAuthentication.Anonymous)
            {
                _logger.LogWarning("No authentication provided for Copy query {QueryId}; connecting using Anonymous authentication. Check inbound endpoint settings.", query.QueryId);
            }
            await _smbClient.ConnectAsync(query.Host, query.Port, query.ShareName, query.Authentication, cancellationToken);

            // Build file path pattern
            var basePath = query.BasePath.TrimEnd('/', '\\');
            var fullPath = $"{basePath}";

            // List files matching the pattern
            // For Copy task, default to copying all files if pattern is the CSV default or empty
            var listPattern = query.FilePattern;
            if (string.IsNullOrWhiteSpace(listPattern) || listPattern == HistorianAssetToQueryMapper.DefaultFilePattern)
            {
                listPattern = "*.*";
            }

            var files = await _smbClient.ListFilesAsync(
                fullPath,
                listPattern,
                cancellationToken);

            _logger.LogInformation("Found {FileCount} files matching pattern {Pattern} for Copy", files.Count, query.FilePattern);

            if (files.Count == 0 && _emptyListingDiagnosticsLogged.TryAdd(query.QueryId, 0))
            {
                await LogEmptyListingDiagnosticsAsync(query, basePath, listPattern, cancellationToken);
            }

            // Track current files for deletion detection
            foreach (var file in files)
            {
                currentFiles.Add(file.Path);
            }

            // Filter files by modification time (only process files modified after watermark)
            var filesToProcess = files
                .Where(f => f.LastModified > watermark)
                .OrderBy(f => f.LastModified)
                .ToList();

            _logger.LogInformation("Copying {FileCount} files modified after watermark", filesToProcess.Count);

            // Process each file
            foreach (var file in filesToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip files that are too large (T023)
                if (file.Size > options.MaxFileSizeBytes)
                {
                    _logger.LogWarning(
                        "Skipping file {Path} (size: {Size} bytes) - exceeds max size {MaxSize} bytes",
                        file.Path,
                        file.Size,
                        options.MaxFileSizeBytes);
                    filesSkipped++;
                    continue;
                }

                // Perform atomic copy (T022)
                var copySuccess = await CopyFileAtomicallyAsync(
                    file,
                    options.DestinationPath,
                    cancellationToken);

                if (copySuccess)
                {
                    filesCopied++;
                    totalBytesCopied += file.Size;

                    // Track the maximum timestamp for watermark update
                    if (!maxTimestamp.HasValue || file.LastModified > maxTimestamp.Value)
                    {
                        maxTimestamp = file.LastModified;
                    }

                    _logger.LogInformation(
                        "Successfully copied file {Path} ({Size} bytes) to {Destination}",
                        file.Path,
                        file.Size,
                        options.DestinationPath);
                }
                else
                {
                    filesFailed++;
                    _logger.LogError("Failed to copy file {Path}", file.Path);
                }
            }

            // Detect source deletions (T023)
            if (previousFiles.Count > 0)
            {
                var deletedFiles = previousFiles.Except(currentFiles).ToList();
                if (deletedFiles.Count > 0)
                {
                    _logger.LogWarning(
                        "Detected {DeletedCount} files deleted from source (one-way sync, local copies retained): {DeletedFiles}",
                        deletedFiles.Count,
                        string.Join(", ", deletedFiles));
                }
            }

            // Log copy results (T023)
            _logger.LogInformation(
                "Copy operation completed for query {QueryId}: {FilesCopied} copied, {FilesSkipped} skipped, {FilesFailed} failed, {TotalBytes} total bytes",
                query.QueryId,
                filesCopied,
                filesSkipped,
                filesFailed,
                totalBytesCopied);

            // Update watermark only if all copies succeeded (T022)
            if (filesFailed == 0 && maxTimestamp.HasValue && maxTimestamp.Value > watermark)
            {
                var newWatermarkData = new WatermarkData
                {
                    QueryId = query.QueryId,
                    Watermark = maxTimestamp.Value,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _watermarkStore.SetAsync($"watermark:{query.QueryId}", newWatermarkData);
                _logger.LogInformation("Updated watermark for Copy query {QueryId} to {Watermark:o}",
                    query.QueryId, maxTimestamp.Value);
            }
            else if (filesFailed > 0)
            {
                _logger.LogWarning(
                    "Watermark not updated due to {FailedCount} failed copies - will retry on next cycle",
                    filesFailed);
            }
        }
        finally
        {
            await _smbClient.DisconnectAsync(cancellationToken);
        }

        _logger.LogDebug(
            "Completed Copy task for query {QueryId} for window [{Start:o}, {End:o})",
            query.QueryId,
            windowStart,
            windowEnd);
    }

    private async Task LogEmptyListingDiagnosticsAsync(
        HistorianQueryDefinition query,
        string basePath,
        string listPattern,
        CancellationToken cancellationToken)
    {
        try
        {
            var basePathExists = await _smbClient.DirectoryExistsAsync(basePath, cancellationToken);
            var rootDirectories = await _smbClient.ListDirectoriesAsync(string.Empty, cancellationToken);
            var rootFiles = await _smbClient.ListFilesAsync(string.Empty, "*.*", cancellationToken);

            _logger.LogWarning(
                "No files were found for query {QueryId}. Diagnostic snapshot: host={Host}, share={Share}, basePath='{BasePath}', basePathExists={BasePathExists}, pattern='{Pattern}', rootDirectoryCount={RootDirectoryCount}, rootDirectories=[{RootDirectories}], rootFileCount={RootFileCount}, sampleRootFiles=[{SampleRootFiles}].",
                query.QueryId,
                query.Host,
                query.ShareName,
                basePath,
                basePathExists,
                listPattern,
                rootDirectories.Count,
                string.Join(", ", rootDirectories.Take(10)),
                rootFiles.Count,
                string.Join(", ", rootFiles.Take(10).Select(f => f.Path)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to collect empty-listing diagnostics for query {QueryId}. host={Host}, share={Share}, basePath='{BasePath}', pattern='{Pattern}'.",
                query.QueryId,
                query.Host,
                query.ShareName,
                basePath,
                listPattern);
        }
    }

    /// <summary>
    /// Copies a file atomically from SMB to local destination (T022).
    /// Uses temp file + fsync + atomic rename pattern.
    /// </summary>
    private async Task<bool> CopyFileAtomicallyAsync(
        FileMetadata file,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(file.Path);
        var finalPath = Path.Combine(destinationPath, fileName);
        var tempPath = $"{finalPath}.tmp";

        try
        {
            // Read file content from SMB
            var content = await _smbClient.ReadFileAsync(file.Path, cancellationToken);

            // Write to temp file
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);

            // Flush to disk (fsync equivalent in .NET)
            using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await fs.FlushAsync(cancellationToken);
            }

            // Atomic rename
            File.Move(tempPath, finalPath, overwrite: true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying file {Path} to {Destination}", file.Path, finalPath);

            // Clean up temp file on failure (T022)
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                    _logger.LogDebug("Cleaned up temp file {TempPath} after failure", tempPath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up temp file {TempPath}", tempPath);
            }

            return false;
        }
    }

    /// <summary>
    /// Parses CSV content and returns historian samples.
    /// Expected CSV format: timestamp,tag,value
    /// </summary>
    private List<HistorianSample> ParseCsvContent(
        string content,
        HistorianQueryDefinition query,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var samples = new List<HistorianSample>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Create a map of tag names to data point names for quick lookup
        var tagMap = query.DataPoints.ToDictionary(
            dp => dp.DataSource ?? dp.Name,
            dp => dp.Name,
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            // Skip header line
            if (line.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 3)
            {
                _logger.LogWarning("Skipping invalid CSV line (expected 3+ columns): {Line}", line);
                continue;
            }

            try
            {
                // Parse timestamp
                if (!DateTimeOffset.TryParse(parts[0].Trim(), out var timestamp))
                {
                    _logger.LogWarning("Skipping line with invalid timestamp: {Line}", line);
                    continue;
                }

                // Check if timestamp is within the query window
                if (timestamp < windowStart || timestamp >= windowEnd)
                {
                    continue;
                }

                // Parse tag name
                var tag = parts[1].Trim();
                if (!tagMap.TryGetValue(tag, out var dataPointName))
                {
                    // Tag not in query, skip
                    continue;
                }

                // Parse value
                if (!double.TryParse(parts[2].Trim(), out var value))
                {
                    _logger.LogWarning("Skipping line with invalid value: {Line}", line);
                    continue;
                }

                // Parse quality (optional, default to 0 = Good)
                var quality = 0;
                if (parts.Length > 3 && int.TryParse(parts[3].Trim(), out var parsedQuality))
                {
                    quality = parsedQuality;
                }

                samples.Add(new HistorianSample
                {
                    DataPointName = dataPointName,
                    TimestampUtc = timestamp,
                    Value = value,
                    Quality = quality,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing CSV line: {Line}", line);
            }
        }

        return samples;
    }
}
