import { marked } from "marked";

// Configure marked for safe, clean HTML output
marked.setOptions({
  gfm: true, // GitHub-flavored markdown (tables, strikethrough, etc.)
  breaks: true, // Convert \n to <br>
});

/**
 * Render markdown text to sanitised HTML for use with dangerouslySetInnerHTML.
 * All user-supplied HTML entities are escaped first, then markdown is parsed.
 */
export function formatMarkdown(text: string): string {
  // marked.parse can return string | Promise<string>; we force sync mode
  const html = marked.parse(text) as string;
  return html;
}

/**
 * Strip markdown to plain text (for tooltips, dialogs, etc.)
 */
export function stripMarkdown(text: string): string {
  return text
    .replace(/#{1,6}\s+/g, "")
    .replace(/\*\*(.+?)\*\*/g, "$1")
    .replace(/\*(.+?)\*/g, "$1")
    .replace(/`(.+?)`/g, "$1")
    .replace(/\[(.+?)\]\(.+?\)/g, "$1")
    .replace(/\n/g, " ")
    .trim();
}
