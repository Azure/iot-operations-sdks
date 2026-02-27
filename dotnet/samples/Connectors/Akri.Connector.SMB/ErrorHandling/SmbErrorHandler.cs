// <copyright file="SmbErrorHandler.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Microsoft.Extensions.Logging;
using SMBLibrary;

namespace Akri.Connector.SMB.ErrorHandling;

/// <summary>
/// SMB error classification and handling.
/// </summary>
public static class SmbErrorHandler
{
    /// <summary>
    /// Error classification categories.
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>
        /// Transient error - retry may succeed.
        /// </summary>
        Transient,

        /// <summary>
        /// Authentication or authorization error.
        /// </summary>
        Authentication,

        /// <summary>
        /// Configuration error - requires user intervention.
        /// </summary>
        Configuration,

        /// <summary>
        /// Network connectivity error.
        /// </summary>
        Network,

        /// <summary>
        /// Resource not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// Permission denied.
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// Fatal error - cannot recover.
        /// </summary>
        Fatal,

        /// <summary>
        /// Unknown error.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// Classifies an SMB NTStatus error.
    /// </summary>
    /// <param name="status">The NT status code.</param>
    /// <returns>The error category.</returns>
    public static ErrorCategory ClassifyNTStatus(NTStatus status)
    {
        return status switch
        {
            // Transient errors - retry may succeed
            NTStatus.STATUS_IO_TIMEOUT => ErrorCategory.Transient,
            NTStatus.STATUS_PENDING => ErrorCategory.Transient,
            NTStatus.STATUS_LOCK_NOT_GRANTED => ErrorCategory.Transient,
            NTStatus.STATUS_FILE_LOCK_CONFLICT => ErrorCategory.Transient,

            // Authentication errors
            NTStatus.STATUS_LOGON_FAILURE => ErrorCategory.Authentication,
            NTStatus.STATUS_ACCOUNT_DISABLED => ErrorCategory.Authentication,
            NTStatus.STATUS_ACCOUNT_EXPIRED => ErrorCategory.Authentication,
            NTStatus.STATUS_ACCOUNT_LOCKED_OUT => ErrorCategory.Authentication,
            NTStatus.STATUS_PASSWORD_EXPIRED => ErrorCategory.Authentication,
            NTStatus.STATUS_PASSWORD_MUST_CHANGE => ErrorCategory.Authentication,
            NTStatus.STATUS_INVALID_LOGON_HOURS => ErrorCategory.Authentication,
            NTStatus.STATUS_INVALID_WORKSTATION => ErrorCategory.Authentication,
            NTStatus.STATUS_WRONG_PASSWORD => ErrorCategory.Authentication,

            // Permission denied
            NTStatus.STATUS_ACCESS_DENIED => ErrorCategory.PermissionDenied,
            NTStatus.STATUS_PRIVILEGE_NOT_HELD => ErrorCategory.PermissionDenied,

            // Network errors
            NTStatus.STATUS_BAD_NETWORK_NAME => ErrorCategory.Network,

            // Not found errors
            NTStatus.STATUS_OBJECT_NAME_NOT_FOUND => ErrorCategory.NotFound,
            NTStatus.STATUS_OBJECT_PATH_NOT_FOUND => ErrorCategory.NotFound,
            NTStatus.STATUS_NO_SUCH_FILE => ErrorCategory.NotFound,

            // Configuration errors
            NTStatus.STATUS_INVALID_PARAMETER => ErrorCategory.Configuration,
            NTStatus.STATUS_INVALID_DEVICE_REQUEST => ErrorCategory.Configuration,
            NTStatus.STATUS_NOT_SUPPORTED => ErrorCategory.Configuration,

            // Success (shouldn't be an error)
            NTStatus.STATUS_SUCCESS => ErrorCategory.Unknown,

            // Default: unknown
            _ => ErrorCategory.Unknown,
        };
    }

    /// <summary>
    /// Classifies a general exception.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>The error category.</returns>
    public static ErrorCategory ClassifyException(Exception exception)
    {
        return exception switch
        {
            TimeoutException => ErrorCategory.Transient,
            System.Net.Sockets.SocketException => ErrorCategory.Network,
            System.IO.IOException => ErrorCategory.Transient,
            UnauthorizedAccessException => ErrorCategory.PermissionDenied,
            InvalidOperationException => ErrorCategory.Configuration,
            ArgumentException => ErrorCategory.Configuration,
            _ => ErrorCategory.Unknown,
        };
    }

    /// <summary>
    /// Determines if an error category is retryable.
    /// </summary>
    /// <param name="category">The error category.</param>
    /// <returns>True if the error is retryable, false otherwise.</returns>
    public static bool IsRetryable(ErrorCategory category)
    {
        return category is ErrorCategory.Transient or ErrorCategory.Network;
    }

    /// <summary>
    /// Logs an SMB error with appropriate log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="status">The NT status code.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="context">Additional context.</param>
    public static void LogNTStatusError(
        ILogger logger,
        NTStatus status,
        string operation,
        string? context = null)
    {
        var category = ClassifyNTStatus(status);
        var logLevel = GetLogLevelForCategory(category);

        var message = context != null
            ? "SMB operation '{Operation}' failed with status {Status} ({Category}): {Context}"
            : "SMB operation '{Operation}' failed with status {Status} ({Category})";

        logger.Log(logLevel, message, operation, status, category, context);
    }

    /// <summary>
    /// Logs an exception error with appropriate log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="context">Additional context.</param>
    public static void LogExceptionError(
        ILogger logger,
        Exception exception,
        string operation,
        string? context = null)
    {
        var category = ClassifyException(exception);
        var logLevel = GetLogLevelForCategory(category);

        var message = context != null
            ? "SMB operation '{Operation}' failed ({Category}): {Context}"
            : "SMB operation '{Operation}' failed ({Category})";

        logger.Log(logLevel, exception, message, operation, category, context);
    }

    private static LogLevel GetLogLevelForCategory(ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Transient => LogLevel.Warning,
            ErrorCategory.Authentication => LogLevel.Error,
            ErrorCategory.Configuration => LogLevel.Error,
            ErrorCategory.Network => LogLevel.Warning,
            ErrorCategory.NotFound => LogLevel.Warning,
            ErrorCategory.PermissionDenied => LogLevel.Error,
            ErrorCategory.Fatal => LogLevel.Critical,
            ErrorCategory.Unknown => LogLevel.Error,
            _ => LogLevel.Error,
        };
    }
}
