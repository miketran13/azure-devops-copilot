import * as SDK from "azure-devops-extension-sdk";
import {
  CommonServiceIds,
  IHostPageLayoutService,
} from "azure-devops-extension-api";

export interface ChatPanelOptions {
  /** Optional initial message to auto-send when the panel opens */
  initialMessage?: string;
  /** Optional work item context for the chat */
  workItemContext?: {
    id: number;
    title: string;
    type: string;
  };
}

/**
 * Opens the DevOps Copilot chat as a slide-out panel from the right side.
 *
 * ADO extensions run in sandboxed iframes, so true floating overlays aren't
 * possible. Instead, this uses the host's IHostPageLayoutService.openPanel()
 * to render the chat in a native ADO panel that slides in over page content,
 * similar to CopilotKit's CopilotSidebar pattern.
 *
 * Can be called from any contribution (Hub, WorkItemGroup, ContextMenuAction).
 */
export async function openChatPanel(
  options: ChatPanelOptions = {},
): Promise<void> {
  const layoutService = await SDK.getService<IHostPageLayoutService>(
    CommonServiceIds.HostPageLayoutService,
  );

  const extensionContext = SDK.getExtensionContext();
  const contributionId = `${extensionContext.publisherId}.${extensionContext.extensionId}.copilot-chat-panel`;

  await layoutService.openPanel(contributionId, {
    title: "DevOps Copilot",
    size: 2, // medium width panel
    configuration: {
      initialMessage: options.initialMessage,
      workItemContext: options.workItemContext,
    },
  });
}
