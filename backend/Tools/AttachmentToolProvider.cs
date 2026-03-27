using DevOpsCopilot.Services;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for attachment operations.
/// </summary>
public sealed class AttachmentToolProvider : IToolProvider
{
    public string ToolGroupName => "attachment";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var attachmentService = new AttachmentService(
            devOpsService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AttachmentService>.Instance);
        var tools = new AttachmentTools(devOpsService, attachmentService);
        return
        [
            AIFunctionFactory.Create(tools.UploadAttachment),
            AIFunctionFactory.Create(tools.CheckFileReadability),
            AIFunctionFactory.Create(tools.ReadAttachmentContent),
        ];
    }
}
