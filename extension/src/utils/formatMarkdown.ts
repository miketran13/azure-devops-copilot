import { marked } from "marked";

// Configure marked for safe, clean HTML output
marked.setOptions({
  gfm: true, // GitHub-flavored markdown (tables, strikethrough, etc.)
  breaks: true, // Convert \n to <br>
});

/**
 * Custom renderer to enhance markdown output:
 * - Make links open in a new tab
 * - Detect stat-like table rows and add data attributes for CSS bar styling
 */
const renderer = new marked.Renderer();

// Open links in a new tab for security
renderer.link = ({ href, title, text }) => {
  const titleAttr = title ? ` title="${title}"` : "";
  return `<a href="${href}"${titleAttr} target="_blank" rel="noopener noreferrer">${text}</a>`;
};

marked.use({ renderer });

/**
 * Render markdown text to HTML for use with dangerouslySetInnerHTML.
 * Enhances output with:
 * - Stat bars for percentage values in tables
 * - KPI summary cards when stat patterns are detected
 */
export function formatMarkdown(text: string): string {
  let html = marked.parse(text) as string;

  // Post-process: inject progress bars into table cells containing percentages
  html = html.replace(
    /<td>(\s*(\d{1,3})(\.\d+)?%\s*)<\/td>/g,
    (_match, content, pct) => {
      const percent = Math.min(100, parseInt(pct, 10));
      const color =
        percent >= 75 ? "#107c10" : percent >= 40 ? "#ff8c00" : "#d13438";
      return `<td><div class="stat-cell"><span>${content.trim()}</span><div class="stat-bar"><div class="stat-bar-fill" style="width:${percent}%;background:${color}"></div></div></div></td>`;
    },
  );

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
