using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Services;

/// <summary>
/// Handles file attachment operations for Azure DevOps work items.
/// Supports upload, content extraction for supported text-based file types, and safe validation.
/// </summary>
public sealed class AttachmentService
{
    private readonly AzureDevOpsService _devOps;
    private readonly ILogger<AttachmentService> _logger;

    /// <summary>Maximum upload size in bytes (10 MB).</summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// File extensions whose content can be read and injected into AI context.
    /// All other file types are upload-only.
    /// </summary>
    private static readonly HashSet<string> ReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".yaml", ".yml",
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java",
        ".html", ".css", ".scss", ".sql", ".sh", ".ps1",
        ".log", ".config", ".env", ".ini", ".toml",
        ".csproj", ".sln", ".props", ".targets",
        ".feature", ".gherkin",
    };

    /// <summary>
    /// Blocked file extensions for security reasons.
    /// </summary>
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".scr",
        ".vbs", ".wsf", ".wsh", ".ps1", // ps1 blocked for upload safety; readable only if from ADO
    };

    /// <summary>
    /// Allowed MIME content types for upload.
    /// </summary>
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/markdown", "text/csv", "text/html", "text/css",
        "application/json", "application/xml", "text/xml",
        "application/pdf",
        "image/png", "image/jpeg", "image/gif", "image/svg+xml",
        "application/zip", "application/x-zip-compressed",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/octet-stream", // generic binary — validated by extension
    };

    public AttachmentService(AzureDevOpsService devOps, ILogger<AttachmentService> logger)
    {
        _devOps = devOps;
        _logger = logger;
    }

    /// <summary>
    /// Validates that a file is safe and allowed for upload.
    /// </summary>
    public AttachmentValidationResult ValidateFile(string fileName, long fileSizeBytes, string? contentType)
    {
        var ext = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(fileName))
            return AttachmentValidationResult.Fail("File name is required.");

        if (fileSizeBytes <= 0)
            return AttachmentValidationResult.Fail("File is empty.");

        if (fileSizeBytes > MaxFileSizeBytes)
            return AttachmentValidationResult.Fail(
                $"File size ({fileSizeBytes / 1024 / 1024} MB) exceeds maximum allowed size ({MaxFileSizeBytes / 1024 / 1024} MB).");

        if (BlockedExtensions.Contains(ext))
            return AttachmentValidationResult.Fail(
                $"File type '{ext}' is not allowed for security reasons.");

        if (!string.IsNullOrEmpty(contentType) && !AllowedContentTypes.Contains(contentType))
        {
            _logger.LogWarning("Unsupported content type '{ContentType}' for file '{FileName}'",
                contentType, fileName);
        }

        return AttachmentValidationResult.Success(IsReadableFileType(ext));
    }

    /// <summary>
    /// Determines whether a file is a supported text-based type whose content can be read into AI context.
    /// </summary>
    public static bool IsReadableFileType(string extensionOrFileName)
    {
        var ext = extensionOrFileName.StartsWith('.')
            ? extensionOrFileName
            : Path.GetExtension(extensionOrFileName);

        return ReadableExtensions.Contains(ext);
    }

    /// <summary>
    /// Extracts text content from a readable file's byte contents.
    /// Returns null for unsupported file types.
    /// </summary>
    public string? ExtractTextContent(string fileName, byte[] fileContent)
    {
        if (!IsReadableFileType(fileName))
        {
            _logger.LogInformation("File '{FileName}' is not a readable type — upload only.", fileName);
            return null;
        }

        try
        {
            var text = Encoding.UTF8.GetString(fileContent);

            // Truncate very large files to avoid flooding the AI context
            const int maxChars = 50_000;
            if (text.Length > maxChars)
            {
                _logger.LogWarning("File '{FileName}' truncated from {Len} to {Max} chars",
                    fileName, text.Length, maxChars);
                text = text[..maxChars] + $"\n\n[... truncated — file is {text.Length:N0} characters]";
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from '{FileName}'", fileName);
            return null;
        }
    }
}

/// <summary>
/// Result of validating an attachment for upload.
/// </summary>
public sealed class AttachmentValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsReadable { get; init; }

    public static AttachmentValidationResult Success(bool isReadable) =>
        new() { IsValid = true, IsReadable = isReadable };

    public static AttachmentValidationResult Fail(string error) =>
        new() { IsValid = false, ErrorMessage = error };
}
