import * as React from "react";
import {
  Button,
  Textarea,
  Dropdown,
  Option,
  Text,
  Tooltip,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  SendRegular,
  ClipboardTaskRegular,
  AttachRegular,
  BranchRegular,
  PersonRegular,
  SearchSparkleRegular,
} from "@fluentui/react-icons";
import { AttachmentPopover } from "./AttachmentPopover";
import { AttachmentChip } from "./AttachmentChip";
import type { AttachmentItem } from "./AttachmentChip";
import type { ModelInfo } from "../models/types";

export interface ChatInputBarProps {
  value: string;
  onChange: (value: string) => void;
  onSend: () => void;
  disabled?: boolean;
  placeholder?: string;
  models: ModelInfo[];
  selectedModelId: string;
  onModelChange: (modelId: string) => void;
  attachments: AttachmentItem[];
  onAddAttachment: (item: AttachmentItem) => void;
  onRemoveAttachment: (id: string) => void;
  /** Search functions for each attachment type */
  searchWorkItems: (query: string) => Promise<AttachmentItem[]>;
  searchMembers: (query: string) => Promise<AttachmentItem[]>;
  searchRepos: (query: string) => Promise<AttachmentItem[]>;
}

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke2),
    backgroundColor: tokens.colorNeutralBackground1,
  },
  attachmentBar: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("4px"),
    ...shorthands.padding("6px", "12px", "0"),
  },
  inputRow: {
    display: "flex",
    alignItems: "flex-end",
    ...shorthands.gap("0"),
    ...shorthands.padding("8px", "12px"),
  },
  inputWrapper: {
    display: "flex",
    flexDirection: "column",
    flexGrow: 1,
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
    ...shorthands.borderRadius("8px"),
    backgroundColor: tokens.colorNeutralBackground3,
    transitionProperty: "border-color",
    transitionDuration: "0.15s",
    "&:focus-within": {
      ...shorthands.borderColor(tokens.colorBrandStroke1),
    },
  },
  textarea: {
    flexGrow: 1,
    "& textarea": {
      ...shorthands.border("none"),
      backgroundColor: "transparent",
      resize: "none",
      ...shorthands.padding("8px", "12px", "4px"),
      fontSize: "14px",
      lineHeight: "1.4",
      minHeight: "24px",
      maxHeight: "120px",
    },
  },
  toolbar: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("2px"),
    ...shorthands.padding("2px", "4px", "4px"),
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke3),
  },
  toolbarSpacer: {
    flexGrow: 1,
  },
  sendButton: {
    minWidth: "32px",
    height: "32px",
    ...shorthands.borderRadius("50%"),
    marginLeft: "8px",
    flexShrink: 0,
  },
  modelDropdown: {
    minWidth: "120px",
    maxWidth: "160px",
  },
  iconBtn: {
    minWidth: "28px",
    width: "28px",
    height: "28px",
    ...shorthands.padding("0"),
  },
  fileInput: {
    display: "none",
  },
});

export function ChatInputBar({
  value,
  onChange,
  onSend,
  disabled,
  placeholder,
  models,
  selectedModelId,
  onModelChange,
  attachments,
  onAddAttachment,
  onRemoveAttachment,
  searchWorkItems,
  searchMembers,
  searchRepos,
}: ChatInputBarProps): React.ReactElement {
  const styles = useStyles();
  const textareaRef = React.useRef<HTMLTextAreaElement>(null);
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      if (value.trim() && !disabled) {
        onSend();
      }
    }
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files) return;

    // Patterns for text-readable file types
    const TEXT_MIME =
      /^text\/|^application\/(json|xml|yaml|javascript|x-sh|x-python|x-ruby|graphql)/;
    const TEXT_EXT =
      /\.(txt|md|json|xml|yaml|yml|js|ts|jsx|tsx|css|scss|html|htm|csv|log|sh|py|cs|java|cpp|c|h|rb|go|rs|php|sql|env|config|gitignore|dockerfile|tf|bicep|ps1)$/i;
    const MAX_BYTES = 500 * 1024; // 500 KB cap

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      const isText = TEXT_MIME.test(file.type) || TEXT_EXT.test(file.name);

      if (isText && file.size <= MAX_BYTES) {
        // Read text content — add to attachment once resolved
        file
          .text()
          .then((content) => {
            onAddAttachment({
              type: "file",
              id: `file-${Date.now()}-${file.name}`,
              label: file.name,
              secondaryLabel: `${(file.size / 1024).toFixed(1)} KB`,
              content,
            });
          })
          .catch(() => {
            onAddAttachment({
              type: "file",
              id: `file-${Date.now()}-${file.name}`,
              label: file.name,
              secondaryLabel: `${(file.size / 1024).toFixed(1)} KB`,
            });
          });
      } else {
        onAddAttachment({
          type: "file",
          id: `file-${Date.now()}-${file.name}`,
          label: file.name,
          secondaryLabel:
            file.size > MAX_BYTES
              ? `Too large to read (${(file.size / 1024 / 1024).toFixed(1)} MB)`
              : `${(file.size / 1024).toFixed(1)} KB · Binary`,
        });
      }
    }
    // Reset input so the same file can be re-selected
    e.target.value = "";
  };

  // Auto-resize textarea
  React.useEffect(() => {
    const el = textareaRef.current;
    if (el) {
      el.style.height = "auto";
      el.style.height = `${Math.min(el.scrollHeight, 120)}px`;
    }
  }, [value]);

  return (
    <div className={styles.container}>
      {/* Attached items */}
      {attachments.length > 0 && (
        <div className={styles.attachmentBar}>
          {attachments.map((att) => (
            <AttachmentChip
              key={att.id}
              attachment={att}
              onRemove={onRemoveAttachment}
            />
          ))}
        </div>
      )}

      <div className={styles.inputRow}>
        <div className={styles.inputWrapper}>
          {/* Textarea */}
          <Textarea
            ref={textareaRef}
            className={styles.textarea}
            value={value}
            onChange={(_e, data) => onChange(data.value)}
            onKeyDown={handleKeyDown}
            placeholder={
              placeholder ??
              "Ask about your work items... (Enter to send, Shift+Enter for new line)"
            }
            resize="none"
            rows={1}
            disabled={disabled}
            appearance="filled-lighter"
          />

          {/* Toolbar with attachment icons + model selector */}
          <div className={styles.toolbar}>
            {/* Work Item picker */}
            <AttachmentPopover
              type="workItem"
              icon={<ClipboardTaskRegular fontSize={16} />}
              tooltip="Attach work item"
              placeholder="Search by ID or title..."
              onSelect={onAddAttachment}
              searchFn={searchWorkItems}
              disabled={disabled}
            />

            {/* File attachment */}
            <Tooltip content="Attach file" relationship="label">
              <Button
                className={styles.iconBtn}
                appearance="subtle"
                icon={<AttachRegular fontSize={16} />}
                size="small"
                disabled={disabled}
                onClick={() => fileInputRef.current?.click()}
              />
            </Tooltip>
            <input
              ref={fileInputRef}
              className={styles.fileInput}
              type="file"
              multiple
              onChange={handleFileSelect}
            />

            {/* Repository picker */}
            <AttachmentPopover
              type="repository"
              icon={<BranchRegular fontSize={16} />}
              tooltip="Attach repository"
              placeholder="Search repositories..."
              onSelect={onAddAttachment}
              searchFn={searchRepos}
              disabled={disabled}
            />

            {/* Member picker */}
            <AttachmentPopover
              type="member"
              icon={<PersonRegular fontSize={16} />}
              tooltip="Mention team member"
              placeholder="Search team members..."
              onSelect={onAddAttachment}
              searchFn={searchMembers}
              disabled={disabled}
            />

            <div className={styles.toolbarSpacer} />

            {/* Model selector */}
            {models.length > 1 && (
              <>
                <SearchSparkleRegular
                  fontSize={14}
                  style={{
                    color: tokens.colorNeutralForeground3,
                    marginRight: "4px",
                  }}
                />
                <Dropdown
                  className={styles.modelDropdown}
                  size="small"
                  value={
                    models.find((m) => m.id === selectedModelId)?.displayName ??
                    "Model"
                  }
                  selectedOptions={[selectedModelId]}
                  onOptionSelect={(_e, data) => {
                    if (data.optionValue) onModelChange(data.optionValue);
                  }}
                  disabled={disabled}
                >
                  {models.map((m) => (
                    <Option key={m.id} value={m.id} text={m.displayName}>
                      <div>
                        <Text size={200} weight="semibold">
                          {m.displayName}
                        </Text>
                        {m.description && (
                          <Text
                            size={100}
                            block
                            style={{
                              color: tokens.colorNeutralForeground3,
                            }}
                          >
                            {m.description}
                          </Text>
                        )}
                      </div>
                    </Option>
                  ))}
                </Dropdown>
              </>
            )}
          </div>
        </div>

        {/* Send button */}
        <Tooltip content="Send message" relationship="label">
          <Button
            className={styles.sendButton}
            appearance="primary"
            icon={<SendRegular />}
            onClick={onSend}
            disabled={!value.trim() || disabled}
          />
        </Tooltip>
      </div>
    </div>
  );
}
