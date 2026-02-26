// <copyright file="ConfigurationValidator.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Microsoft.Extensions.Options;

namespace Akri.Connector.SMB.Configuration;

/// <summary>
/// Validates SMB connector configuration options.
/// </summary>
public sealed class ConfigurationValidator : IValidateOptions<SMBConnectorOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SMBConnectorOptions options)
    {
        var errors = new List<string>();

        // Authentication is resolved from ADR inbound endpoints, so local
        // credentials are not validated here.

        // Validate connection timeout
        if (options.ConnectionTimeoutSeconds < 1 || options.ConnectionTimeoutSeconds > 300)
        {
            errors.Add("ConnectionTimeoutSeconds must be between 1 and 300");
        }

        // Validate max concurrent connections
        if (options.MaxConcurrentConnections < 1 || options.MaxConcurrentConnections > 100)
        {
            errors.Add("MaxConcurrentConnections must be between 1 and 100");
        }

        // Validate max file size
        if (options.MaxFileSizeBytes < 1 || options.MaxFileSizeBytes > 104857600)
        {
            errors.Add("MaxFileSizeBytes must be between 1 and 104857600 (100MB)");
        }

        // Validate leader election settings if enabled
        if (options.EnableLeaderElection)
        {
            if (string.IsNullOrWhiteSpace(options.LeaderElectionNamespace))
            {
                errors.Add("LeaderElectionNamespace is required when EnableLeaderElection is true");
            }

            if (string.IsNullOrWhiteSpace(options.LeaderElectionLeaseName))
            {
                errors.Add("LeaderElectionLeaseName is required when EnableLeaderElection is true");
            }
        }

        // Validate instance ID
        if (string.IsNullOrWhiteSpace(options.InstanceId))
        {
            errors.Add("InstanceId is required and cannot be empty");
        }

        // Validate task type
        var validTaskTypes = new[] { "Parse", "Copy" };
        if (!validTaskTypes.Contains(options.TaskType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"TaskType must be one of: {string.Join(", ", validTaskTypes)}");
        }

        // Validate destination path for Copy task type
        if (string.Equals(options.TaskType, "Copy", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.DestinationPath))
            {
                errors.Add("DestinationPath is required when TaskType is 'Copy'");
            }
            else
            {
                // Validate destination path exists and is writable at startup
                if (!Directory.Exists(options.DestinationPath))
                {
                    errors.Add($"DestinationPath '{options.DestinationPath}' does not exist");
                }
                else
                {
                    // Test writability by attempting to create a temp file
                    var testFilePath = Path.Combine(options.DestinationPath, $".writetest_{Guid.NewGuid():N}");
                    try
                    {
                        File.WriteAllText(testFilePath, "test");
                        File.Delete(testFilePath);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"DestinationPath '{options.DestinationPath}' is not writable: {ex.Message}");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
