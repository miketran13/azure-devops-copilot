import * as SDK from "azure-devops-extension-sdk";
import {
  IWorkItemFormNavigationService,
  WorkItemTrackingServiceIds,
  IWorkItemFormService,
} from "azure-devops-extension-api/WorkItemTracking";
import { openChatPanel } from "../utils/openChatPanel";

/**
 * Context menu action handlers for work items.
 * Opens the Copilot chat sidebar panel with the appropriate initial message,
 * providing the full streaming chat experience (like CopilotKit's CopilotSidebar).
 */

interface ActionContext {
  workItemIds?: number[];
  id?: number;
}

/** Resolve the first work item's context (id, title, type) from action context */
async function getWorkItemContext(
  context: ActionContext,
): Promise<{ id: number; title: string; type: string } | undefined> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return undefined;

  try {
    // Try to get work item details via the form service (available when on a WI form)
    const formService = await SDK.getService<IWorkItemFormService>(
      WorkItemTrackingServiceIds.WorkItemFormService,
    );
    const fields = await formService.getFieldValues([
      "System.WorkItemType",
      "System.Title",
    ]);
    return {
      id: ids[0],
      title: (fields["System.Title"] as string) ?? "",
      type: (fields["System.WorkItemType"] as string) ?? "",
    };
  } catch {
    // Not on a form — use just the ID
    return { id: ids[0], title: "", type: "" };
  }
}

/**
 * Handler for "Analyze with AI" context menu action.
 * Opens the chat sidebar with an analyze prompt.
 */
async function handleAnalyze(context: ActionContext): Promise<void> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return;

  const wiContext = await getWorkItemContext(context);

  if (ids.length === 1) {
    await openChatPanel({
      initialMessage: "Analyze the quality and completeness of this work item",
      workItemContext: wiContext,
    });
  } else {
    await openChatPanel({
      initialMessage: `Analyze these work items for quality and completeness: ${ids.map((id) => `#${id}`).join(", ")}`,
      workItemContext: wiContext,
    });
  }
}

/**
 * Handler for "Generate Test Cases" context menu action.
 * Opens the chat sidebar with a test case generation prompt.
 */
async function handleGenerateTestCases(context: ActionContext): Promise<void> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return;

  await openChatPanel({
    initialMessage: "Generate test cases for this work item",
    workItemContext: await getWorkItemContext(context),
  });
}

/**
 * Handler for "Suggest Child Items" context menu action.
 * Opens the chat sidebar with a child-item suggestion prompt.
 */
async function handleSuggestChildren(context: ActionContext): Promise<void> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return;

  await openChatPanel({
    initialMessage: "Suggest child items for this work item",
    workItemContext: await getWorkItemContext(context),
  });
}

// Register handlers with the SDK
SDK.init().then(() => {
  SDK.ready().then(() => {
    SDK.register("copilot-analyze-action", {
      execute: handleAnalyze,
    });

    SDK.register("copilot-testcases-action", {
      execute: handleGenerateTestCases,
    });

    SDK.register("copilot-suggest-children-action", {
      execute: handleSuggestChildren,
    });
  });
});
