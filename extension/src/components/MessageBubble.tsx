import * as React from "react";
import { formatMarkdown } from "../utils/formatMarkdown";
import "../../src/styles/markdown.scss";
import "./MessageBubble.scss";

interface MessageBubbleProps {
  role: "user" | "assistant";
  content: string;
  timestamp: Date;
}

/**
 * Renders a single chat message bubble with full markdown support.
 */
export function MessageBubble({
  role,
  content,
  timestamp,
}: MessageBubbleProps): React.ReactElement {
  const formattedTime = timestamp.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });

  return (
    <div className={`message-bubble ${role}`}>
      <div className="bubble-header">
        <span className="bubble-role">
          {role === "user" ? "You" : "Copilot"}
        </span>
        <span className="bubble-time">{formattedTime}</span>
      </div>
      <div
        className="bubble-content md-content"
        dangerouslySetInnerHTML={{ __html: formatMarkdown(content) }}
      />
    </div>
  );
}
