/**
 * Shared utilities for parsing assistant message patterns.
 * Extracted from duplicated logic in ChatPanel.tsx and WorkItemGroup.tsx.
 */

/**
 * Detect if an assistant message is asking for write-action confirmation.
 * Returns false if the message uses the newer **Actions:** pattern.
 */
export function isConfirmationPrompt(content: string): boolean {
  // If the message has an explicit **Actions:** line, the actions system handles it
  if (/\*\*Actions:\*\*/.test(content)) return false;

  const patterns = [
    /shall i (go ahead|proceed|create|update|delete|assign|close|move)/i,
    /would you like me to (create|update|delete|assign|close|move|proceed)/i,
    /do you want me to (create|update|delete|assign|close|move|proceed)/i,
    /ready to (create|update|delete|assign|close|move|proceed)/i,
    /confirm (this|the) (creation|update|change)/i,
    /go ahead\?/i,
    /proceed\?/i,
  ];
  return patterns.some((p) => p.test(content));
}

/**
 * Strip the **Actions:** line from content before rendering in the bubble.
 * The options are shown as interactive buttons, so rendering the raw line
 * would cause visible duplication.
 */
export function stripActionsLine(content: string): string {
  return content.replace(/\n?\*\*Actions:\*\*.*$/m, "").trimEnd();
}

/**
 * Extract selectable action options from assistant messages.
 * Detects two patterns:
 * 1. **Actions:** `Option A` · `Option B` — explicit action line
 * 2. Numbered lists (1. **Item**) in responses with a question mark
 */
export function extractInlineOptions(content: string): string[] | null {
  // Pattern 1: **Actions:** `Option A` · `Option B`
  const actionsMatch = content.match(/\*\*Actions:\*\*\s*(.+)$/m);
  if (actionsMatch) {
    const actionsLine = actionsMatch[1];
    const opts = [...actionsLine.matchAll(/`([^`]+)`/g)]
      .map((m) => m[1].trim())
      .filter((v) => v.length > 0 && v.length < 100);
    if (opts.length >= 2) return opts;
  }

  // Pattern 2: Numbered list with a question
  if (!content.includes("?")) return null;
  const pattern = /^\s*\d+\.\s+\*{0,2}(.+?)\*{0,2}\s*$/gm;
  const matches = [...content.matchAll(pattern)];
  if (matches.length < 2) return null;
  return matches
    .map((m) => m[1].trim())
    .filter((v) => v.length > 0 && v.length < 100);
}
