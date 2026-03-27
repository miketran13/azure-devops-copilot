import * as React from "react";
import {
  Card,
  Text,
  Button,
  Tooltip,
  makeStyles,
  tokens,
  shorthands,
  mergeClasses,
} from "@fluentui/react-components";
import {
  PersonRegular,
  BotRegular,
  CopyRegular,
  CheckmarkRegular,
  ThumbLikeRegular,
  ThumbDislikeRegular,
  ArrowSyncRegular,
} from "@fluentui/react-icons";
import { formatMarkdown } from "../utils/formatMarkdown";
import "../../src/styles/markdown.scss";
import "./MessageBubble.scss";

interface MessageBubbleProps {
  role: "user" | "assistant";
  content: string;
  timestamp: Date;
  /** Callback when user clicks regenerate on an assistant message */
  onRegenerate?: () => void;
}

const useStyles = makeStyles({
  wrapper: {
    display: "flex",
    flexDirection: "column",
    animationName: {
      from: { opacity: 0, transform: "translateY(8px)" },
      to: { opacity: 1, transform: "translateY(0)" },
    },
    animationDuration: "0.15s",
    animationTimingFunction: "ease-out",
    animationFillMode: "both",
  },
  bubble: {
    ...shorthands.padding("10px", "14px"),
    ...shorthands.borderRadius("12px"),
    fontSize: "14px",
    lineHeight: "1.5",
    maxWidth: "100%",
    wordBreak: "break-word",
  },
  user: {
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    borderBottomRightRadius: "4px",
  },
  assistant: {
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground1,
    borderBottomLeftRadius: "4px",
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: "4px",
    opacity: 0.85,
    width: "100%",
    gap: "8px",
  },
  roleLabel: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("6px"),
  },
  avatar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    width: "20px",
    height: "20px",
    ...shorthands.borderRadius("50%"),
    flexShrink: 0,
    fontSize: "10px",
    fontWeight: 600 as unknown as string,
  },
  avatarUser: {
    backgroundColor: "rgba(255,255,255,0.25)",
    color: "inherit",
  },
  avatarBot: {
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
  },
  copyButton: {
    opacity: 0,
    transitionProperty: "opacity",
    transitionDuration: "0.1s",
    marginTop: "4px",
    alignSelf: "flex-end",
  },
  copyVisible: {
    opacity: 1,
  },
  actionBar: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("2px"),
    opacity: 0,
    transitionProperty: "opacity",
    transitionDuration: "0.1s",
    marginTop: "4px",
  },
  actionBarVisible: {
    opacity: 1,
  },
  feedbackActive: {
    color: tokens.colorBrandForeground1,
  },
});

/**
 * Renders a single chat message bubble with avatar, markdown, copy button, and fade-in animation.
 */
export function MessageBubble({
  role,
  content,
  timestamp,
  onRegenerate,
}: MessageBubbleProps): React.ReactElement {
  const styles = useStyles();
  const [hovered, setHovered] = React.useState(false);
  const [copied, setCopied] = React.useState(false);
  const [feedback, setFeedback] = React.useState<"up" | "down" | null>(null);
  const formattedTime = timestamp.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });

  const handleCopy = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await navigator.clipboard.writeText(content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard not available in sandboxed iframe — silent fail
    }
  };

  return (
    <div
      className={styles.wrapper}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div
        className={mergeClasses(
          styles.bubble,
          role === "user" ? styles.user : styles.assistant,
        )}
      >
        <div className={styles.header}>
          <span className={styles.roleLabel}>
            <span
              className={mergeClasses(
                styles.avatar,
                role === "user" ? styles.avatarUser : styles.avatarBot,
              )}
            >
              {role === "user" ? (
                <PersonRegular fontSize={12} />
              ) : (
                <BotRegular fontSize={12} />
              )}
            </span>
            <Text
              size={100}
              weight="semibold"
              style={{ textTransform: "uppercase", letterSpacing: "0.5px" }}
            >
              {role === "user" ? "You" : "Copilot"}
            </Text>
          </span>
          <Text size={100}>{formattedTime}</Text>
        </div>
        <div
          className="bubble-content md-content"
          dangerouslySetInnerHTML={{ __html: formatMarkdown(content) }}
        />
      </div>
      {role === "assistant" && (
        <div
          className={mergeClasses(
            styles.actionBar,
            hovered || copied || feedback ? styles.actionBarVisible : undefined,
          )}
        >
          <Tooltip
            content={copied ? "Copied!" : "Copy message"}
            relationship="label"
          >
            <Button
              appearance="subtle"
              size="small"
              icon={copied ? <CheckmarkRegular /> : <CopyRegular />}
              onClick={handleCopy}
            />
          </Tooltip>
          <Tooltip content="Good response" relationship="label">
            <Button
              appearance="subtle"
              size="small"
              icon={<ThumbLikeRegular />}
              className={feedback === "up" ? styles.feedbackActive : undefined}
              onClick={() =>
                setFeedback((prev) => (prev === "up" ? null : "up"))
              }
            />
          </Tooltip>
          <Tooltip content="Bad response" relationship="label">
            <Button
              appearance="subtle"
              size="small"
              icon={<ThumbDislikeRegular />}
              className={
                feedback === "down" ? styles.feedbackActive : undefined
              }
              onClick={() =>
                setFeedback((prev) => (prev === "down" ? null : "down"))
              }
            />
          </Tooltip>
          {onRegenerate && (
            <Tooltip content="Regenerate response" relationship="label">
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowSyncRegular />}
                onClick={onRegenerate}
              />
            </Tooltip>
          )}
        </div>
      )}
    </div>
  );
}
