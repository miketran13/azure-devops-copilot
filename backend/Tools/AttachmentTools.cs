using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for managing work item attachments.
/// Supports upload, linking to work items, and reading text-based file content.
/// </summary>
public sealed class AttachmentTools
{
    private readonly AzureDevOpsService _devOps;
    private readonly AttachmentService _attachmentService;

    public AttachmentTools(AzureDevOpsService devOps, AttachmentService attachmentService)
    {
        _devOps = devOps;
        _attachmentService = attachmentService;
    }

    [Description("Upload a file attachment to an Azure DevOps work item. " +
        "Provide the file name, content as base64-encoded string, and the work item ID to attach to. " +
        "Supported file types include text files, documents, images, and archives. " +
        "Executable files (.exe, .dll, .bat, etc.) are blocked for security.")]
    public async Task<string> UploadAttachment(
        [Description("Target work item ID to attach the file to")] int workItemId,
        [Description("File name with extension (e.g. 'spec.md', 'screenshot.png')")] string fileName,
        [Description("File content as base64-encoded string")] string base64Content,
        [Description("Optional comment for the attachment")] string? comment = null)
    {
        try
        {
            byte[] fileContent;
            try
            {
                fileContent = Convert.FromBase64String(base64Content);
            }
            catch (FormatException)
            {
                return "ERROR: Invalid base64 content. The file content must be base64-encoded.";
            }

            // Validate the file
            var validation = _attachmentService.ValidateFile(fileName, fileContent.Length, null);
            if (!validation.IsValid)
                return $"ERROR: {validation.ErrorMessage}";

            // Upload to ADO
            var attachmentUrl = await _devOps.UploadAttachmentAsync(fileName, fileContent);

            // Link to work item
            var result = await _devOps.AddAttachmentToWorkItemAsync(
                workItemId, attachmentUrl, fileName, comment);

            var readableNote = validation.IsReadable
                ? " The file content can be read for AI context."
                : " This file type is upload-only (content cannot be read into AI context).";

            return $"Successfully attached '{fileName}' to work item #{result.Id}.{readableNote}";
        }
        catch (Exception ex)
        {
            return $"ERROR uploading attachment: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Check if a file type is supported for content reading (text extraction into AI context). " +
        "Returns whether the file can be read as text or is upload-only.")]
    public Task<string> CheckFileReadability(
        [Description("File name or extension to check (e.g. 'report.md', '.pdf')")] string fileNameOrExtension)
    {
        var isReadable = AttachmentService.IsReadableFileType(fileNameOrExtension);
        var ext = Path.GetExtension(fileNameOrExtension);

        return Task.FromResult(isReadable
            ? $"Files with extension '{ext}' can be read as text and used as AI context."
            : $"Files with extension '{ext}' can be uploaded as attachments but their content cannot be read into AI context.");
    }

    [Description("Read the text content of a work item's attachment. " +
        "Only works for text-based files (.txt, .md, .csv, .json, .xml, .log, .yml, .yaml, .cs, .js, .ts, .py, etc.). " +
        "First call GetWorkItemLinks to find the attachment URL, then pass it here. " +
        "Returns the file content as text, truncated to 50,000 characters.")]
    public async Task<string> ReadAttachmentContent(
        [Description("The full attachment URL from the work item's relations")] string attachmentUrl,
        [Description("The file name (used to check readability)")] string fileName)
    {
        try
        {
            if (!AttachmentService.IsReadableFileType(fileName))
            {
                var ext = Path.GetExtension(fileName);
                return $"ERROR: Files with extension '{ext}' cannot be read as text. " +
                       "Only text-based file types are supported.";
            }

            var bytes = await _devOps.DownloadAttachmentAsync(attachmentUrl);
            var text = System.Text.Encoding.UTF8.GetString(bytes);

            const int maxChars = 50_000;
            if (text.Length > maxChars)
                text = text[..maxChars] + $"\n\n... [truncated, showing first {maxChars:N0} of {text.Length:N0} characters]";

            return $"Content of '{fileName}' ({bytes.Length:N0} bytes):\n\n{text}";
        }
        catch (Exception ex)
        {
            return $"ERROR reading attachment: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
