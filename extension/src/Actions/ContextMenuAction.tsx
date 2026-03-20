import * as SDK from "azure-devops-extension-sdk";
import {
  IWorkItemFormNavigationService,
  WorkItemTrackingServiceIds,
} from "azure-devops-extension-api/WorkItemTracking";
import {
  analyzeWorkItem,
  generateTestCases,
  suggestChildItems,
  chat,
} from "../services/backendApi";
import { formatMarkdown } from "../utils/formatMarkdown";
import "../styles/markdown.scss";

/**
 * Context menu action handlers for work items.
 * Registered as contributions in the extension manifest for:
 * - work-item-context-menu
 * - backlog-item-menu
 * - query-results-toolbar-menu
 */

interface ActionContext {
  workItemIds?: number[];
  id?: number;
}

/**
 * Handler for "Analyze with AI" context menu action.
 */
async function handleAnalyze(context: ActionContext): Promise<void> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return;

  try {
    if (ids.length === 1) {
      const response = await analyzeWorkItem(ids[0]);
      showResultDialog("AI Analysis", response.reply);
    } else {
      const response = await chat(
        `Analyze these work items for quality and completeness: ${ids.map((id) => `#${id}`).join(", ")}`,
      );
      showResultDialog("Bulk AI Analysis", response.reply);
    }
  } catch (err) {
    const msg = err instanceof Error ? err.message : "Unknown error";
    showResultDialog("Error", `Failed to analyze: ${msg}`);
  }
}

/**
 * Handler for "Generate Test Cases" context menu action.
 */
async function handleGenerateTestCases(context: ActionContext): Promise<void> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return;

  try {
    const response = await generateTestCases(ids[0]);
    showResultDialog("Generated Test Cases", response.reply);
  } catch (err) {
    const msg = err instanceof Error ? err.message : "Unknown error";
    showResultDialog("Error", `Failed to generate test cases: ${msg}`);
  }
}

/**
 * Handler for "Suggest Child Items" context menu action.
 */
async function handleSuggestChildren(context: ActionContext): Promise<void> {
  const ids = context.workItemIds ?? (context.id ? [context.id] : []);
  if (ids.length === 0) return;

  try {
    const response = await suggestChildItems(ids[0]);
    showResultDialog("Suggested Child Items", response.reply);
  } catch (err) {
    const msg = err instanceof Error ? err.message : "Unknown error";
    showResultDialog("Error", `Failed to suggest child items: ${msg}`);
  }
}

/**
 * Show a simple result dialog with the AI response.
 */
function showResultDialog(title: string, content: string): void {
  // Create a floating panel overlay with full markdown rendering
  const overlay = document.createElement("div");
  overlay.className = "action-overlay";
  overlay.innerHTML = `
    <div class="action-dialog">
      <div class="action-dialog-header">
        <h3>${title}</h3>
        <button class="action-dialog-close" title="Close">&times;</button>
      </div>
      <div class="action-dialog-content md-content">${formatMarkdown(content)}</div>
    </div>
  `;

  overlay.addEventListener("click", (e) => {
    if (
      e.target === overlay ||
      (e.target as HTMLElement).classList.contains("action-dialog-close")
    ) {
      overlay.remove();
    }
  });

  document.body.appendChild(overlay);
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
