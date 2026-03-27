import * as React from "react";
import {
  Button,
  Textarea,
  Tooltip,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  TextBoldRegular,
  TextItalicRegular,
  TextHeader1Regular,
  TextBulletListRegular,
  TextNumberListLtrRegular,
  LinkRegular,
  CodeRegular,
  TextQuoteRegular,
} from "@fluentui/react-icons";
import { formatMarkdown } from "../utils/formatMarkdown";
import "../styles/markdown.scss";

export interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minRows?: number;
  maxHeight?: string;
  disabled?: boolean;
}

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
    ...shorthands.borderRadius("6px"),
    ...shorthands.overflow("hidden"),
    "&:focus-within": {
      ...shorthands.borderColor(tokens.colorBrandStroke1),
    },
  },
  tabBar: {
    display: "flex",
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke2),
    backgroundColor: tokens.colorNeutralBackground3,
  },
  tab: {
    ...shorthands.padding("6px", "12px"),
    fontSize: "13px",
    cursor: "pointer",
    ...shorthands.borderBottom("2px", "solid", "transparent"),
    color: tokens.colorNeutralForeground3,
    transitionProperty: "color, border-color",
    transitionDuration: "0.15s",
    "&:hover": {
      color: tokens.colorNeutralForeground1,
    },
  },
  tabActive: {
    ...shorthands.padding("6px", "12px"),
    fontSize: "13px",
    cursor: "pointer",
    color: tokens.colorNeutralForeground1,
    ...shorthands.borderBottom("2px", "solid", tokens.colorBrandStroke1),
    fontWeight: 600 as unknown as string,
  },
  toolbar: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("2px"),
    ...shorthands.padding("4px", "8px"),
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke3),
    backgroundColor: tokens.colorNeutralBackground3,
  },
  toolBtn: {
    minWidth: "26px",
    width: "26px",
    height: "26px",
    ...shorthands.padding("0"),
  },
  textarea: {
    flexGrow: 1,
    "& textarea": {
      ...shorthands.border("none"),
      backgroundColor: "transparent",
      ...shorthands.padding("8px", "12px"),
      fontSize: "13px",
      lineHeight: "1.5",
      resize: "vertical",
    },
  },
  preview: {
    ...shorthands.padding("8px", "12px"),
    minHeight: "80px",
    overflowY: "auto",
    fontSize: "13px",
    lineHeight: "1.5",
  },
});

export function MarkdownEditor({
  value,
  onChange,
  placeholder,
  minRows = 4,
  maxHeight = "300px",
  disabled,
}: MarkdownEditorProps): React.ReactElement {
  const styles = useStyles();
  const [activeTab, setActiveTab] = React.useState<"write" | "preview">(
    "write",
  );
  const textareaRef = React.useRef<HTMLTextAreaElement>(null);

  const insertMarkdown = (before: string, after: string = "") => {
    const ta = textareaRef.current;
    if (!ta) return;
    const start = ta.selectionStart;
    const end = ta.selectionEnd;
    const selected = value.substring(start, end);
    const newText =
      value.substring(0, start) +
      before +
      (selected || "text") +
      after +
      value.substring(end);
    onChange(newText);
    // Re-focus after state update
    setTimeout(() => {
      ta.focus();
      const newCursor = start + before.length + (selected.length || 4);
      ta.setSelectionRange(newCursor, newCursor);
    }, 0);
  };

  const toolbarButtons = [
    {
      icon: <TextHeader1Regular fontSize={14} />,
      tooltip: "Heading",
      action: () => insertMarkdown("### "),
    },
    {
      icon: <TextBoldRegular fontSize={14} />,
      tooltip: "Bold",
      action: () => insertMarkdown("**", "**"),
    },
    {
      icon: <TextItalicRegular fontSize={14} />,
      tooltip: "Italic",
      action: () => insertMarkdown("_", "_"),
    },
    {
      icon: <TextBulletListRegular fontSize={14} />,
      tooltip: "Bulleted list",
      action: () => insertMarkdown("- "),
    },
    {
      icon: <TextNumberListLtrRegular fontSize={14} />,
      tooltip: "Numbered list",
      action: () => insertMarkdown("1. "),
    },
    {
      icon: <LinkRegular fontSize={14} />,
      tooltip: "Link",
      action: () => insertMarkdown("[", "](url)"),
    },
    {
      icon: <CodeRegular fontSize={14} />,
      tooltip: "Code",
      action: () => insertMarkdown("`", "`"),
    },
    {
      icon: <TextQuoteRegular fontSize={14} />,
      tooltip: "Quote",
      action: () => insertMarkdown("> "),
    },
  ];

  return (
    <div className={styles.container}>
      {/* Tab bar */}
      <div className={styles.tabBar}>
        <span
          className={activeTab === "write" ? styles.tabActive : styles.tab}
          onClick={() => setActiveTab("write")}
          role="tab"
          tabIndex={0}
          onKeyDown={(e) => e.key === "Enter" && setActiveTab("write")}
        >
          Write
        </span>
        <span
          className={activeTab === "preview" ? styles.tabActive : styles.tab}
          onClick={() => setActiveTab("preview")}
          role="tab"
          tabIndex={0}
          onKeyDown={(e) => e.key === "Enter" && setActiveTab("preview")}
        >
          Preview
        </span>
      </div>

      {/* Formatting toolbar (write mode only) */}
      {activeTab === "write" && (
        <div className={styles.toolbar}>
          {toolbarButtons.map((btn) => (
            <Tooltip
              key={btn.tooltip}
              content={btn.tooltip}
              relationship="label"
            >
              <Button
                className={styles.toolBtn}
                appearance="subtle"
                icon={btn.icon}
                size="small"
                onClick={btn.action}
                disabled={disabled}
              />
            </Tooltip>
          ))}
        </div>
      )}

      {/* Content area */}
      {activeTab === "write" ? (
        <Textarea
          ref={textareaRef}
          className={styles.textarea}
          value={value}
          onChange={(_e, data) => onChange(data.value)}
          placeholder={placeholder ?? "Enter markdown..."}
          rows={minRows}
          resize="vertical"
          disabled={disabled}
          appearance="filled-lighter"
          style={{ maxHeight }}
        />
      ) : (
        <div
          className={`${styles.preview} md-content`}
          style={{ maxHeight }}
          dangerouslySetInnerHTML={{
            __html: value
              ? formatMarkdown(value)
              : '<span style="color: var(--colorNeutralForeground3)">Nothing to preview</span>',
          }}
        />
      )}
    </div>
  );
}
