import * as React from "react";
import {
  Button,
  Spinner,
  MessageBar,
  MessageBarBody,
  MessageBarActions,
  Text,
  Tooltip,
  Dropdown,
  Option,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  DismissRegular,
  HistoryRegular,
  CheckmarkRegular,
  EditRegular,
  DismissCircleRegular,
  SearchSparkleRegular,
  DocumentRegular,
  BotSparkleRegular,
  CodeRegular,
  BeakerRegular,
  DataBarVerticalRegular,
  ClipboardTaskRegular,
  BugRegular,
  BookRegular,
  TaskListSquareAddRegular,
  TextBulletListSquareRegular,
  AddRegular,
} from "@fluentui/react-icons";
import { chatStream, fetchModels } from "../services/backendApi";
import {
  searchWorkItems as apiSearchWorkItems,
  searchRepositories as apiSearchRepositories,
  searchMembers as apiSearchMembers,
} from "../services/attachmentSearchApi";
import { createSession, addMessage } from "../services/sessionApi";
import { getDevOpsContext } from "../services/devopsContext";
import { listProjects } from "../services/projectApi";
import type {
  ConversationMessage,
  WorkItemSummary,
  StreamEvent,
  ProcessingStep,
  ModelInfo,
} from "../models/types";
import { MessageBubble } from "./MessageBubble";
import { WorkItemCard } from "./WorkItemCard";
import { SessionPanel } from "./SessionPanel";
import { ChatInputBar } from "./ChatInputBar";
import { ThinkingIndicator } from "./ThinkingIndicator";
import type { AttachmentItem } from "./AttachmentChip";
import type {
  WorkItemFormData,
  PullRequestFormData,
} from "../hooks/usePreviewPanel";
import {
  isConfirmationPrompt,
  stripActionsLine,
  extractInlineOptions,
} from "../utils/chatParsing";

import "./ChatPanel.scss";

/** Maps a WorkItemSummary from the AI response to WorkItemFormData for the preview panel */
function mapWorkItemToFormData(wi: WorkItemSummary): WorkItemFormData {
  return {
    workItemType: wi.workItemType,
    title: wi.title,
    description: wi.description,
    state: wi.state,
    assignedTo: wi.assignedTo,
    areaPath: wi.areaPath,
    iterationPath: wi.iterationPath,
    priority: wi.priority,
    storyPoints: wi.storyPoints,
    tags: wi.tags,
    valueArea: wi.valueArea,
    customFields: wi.customFields,
  };
}

interface ChatMessage {
  role: "user" | "assistant";
  content: string;
  workItems?: WorkItemSummary[];
  suggestedActions?: string[];
  timestamp: Date;
}

const useStyles = makeStyles({
  suggestionChips: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("6px"),
    ...shorthands.padding("6px", "16px"),
  },
  welcomeContainer: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    flexGrow: 1,
    ...shorthands.padding("24px"),
    textAlign: "center" as const,
  },
  welcomeIcon: {
    color: tokens.colorBrandForeground1,
    marginBottom: "8px",
  },
  welcomeGrid: {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    ...shorthands.gap("12px"),
    width: "100%",
    maxWidth: "640px",
    marginTop: "16px",
  },
  personaCard: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.padding("14px"),
    ...shorthands.borderRadius("10px"),
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
    backgroundColor: tokens.colorNeutralBackground1,
    textAlign: "left" as const,
    transitionProperty: "box-shadow, border-color",
    transitionDuration: "0.15s",
    "&:hover": {
      boxShadow: tokens.shadow4,
      ...shorthands.borderColor(tokens.colorBrandStroke1),
    },
  },
  personaHeader: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
    marginBottom: "8px",
  },
  personaIconPo: { color: "#773b93" },
  personaIconDev: { color: "#0078d4" },
  personaIconTest: { color: "#004b50" },
  personaIconMgmt: { color: "#ff7b00" },
  personaChips: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("4px"),
  },
  personaChip: {
    justifyContent: "flex-start",
    fontSize: "12px",
    fontWeight: 400 as unknown as string,
  },
  wiWelcomeHeader: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("10px"),
    ...shorthands.padding("12px"),
    ...shorthands.borderRadius("10px"),
    backgroundColor: tokens.colorNeutralBackground3,
    marginBottom: "12px",
    width: "100%",
    maxWidth: "540px",
    textAlign: "left" as const,
  },
  wiChipGrid: {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    ...shorthands.gap("6px"),
    width: "100%",
    maxWidth: "540px",
  },
  actionButtons: {
    display: "flex",
    ...shorthands.gap("6px"),
    ...shorthands.padding("4px", "0"),
    marginTop: "4px",
  },
  optionButtons: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("6px"),
    ...shorthands.padding("6px", "0"),
    marginTop: "4px",
  },
  toolbarRow: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
    ...shorthands.padding("6px", "16px"),
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke2),
    backgroundColor: tokens.colorNeutralBackground2,
    flexShrink: 0,
  },
  modelDropdown: {
    minWidth: "160px",
    maxWidth: "220px",
  },
  streamingBubble: {
    ...shorthands.padding("10px", "14px"),
    ...shorthands.borderRadius("12px"),
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground1,
    borderBottomLeftRadius: "4px",
    maxWidth: "100%",
    wordBreak: "break-word",
    fontSize: "14px",
    lineHeight: "1.5",
  },
  contextBanner: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("10px"),
    ...shorthands.padding("10px", "16px"),
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke2),
    backgroundColor: tokens.colorNeutralBackground2,
    flexShrink: 0,
  },
  contextIcon: {
    color: tokens.colorBrandForeground1,
    flexShrink: 0,
  },
  contextInfo: {
    display: "flex",
    flexDirection: "column",
    minWidth: 0,
  },
  contextTitle: {
    whiteSpace: "nowrap",
    ...shorthands.overflow("hidden"),
    textOverflow: "ellipsis",
  },
});

export interface ChatPanelProps {
  /** Optional initial message to auto-send when the panel opens */
  initialMessage?: string;
  /** Optional work item context prefix injected before the first message */
  workItemContext?: {
    id: number;
    title: string;
    type: string;
  };
  /** Pre-loaded session from sidebar */
  loadedSession?: {
    messages: ConversationMessage[];
    sessionId: string;
  };
  /** Selected project (from sidebar) */
  selectedProject?: string;
  /** Callback to open the preview panel */
  onOpenPreview?: (
    type: "workItem" | "pullRequest",
    data?: WorkItemFormData | PullRequestFormData,
    editingId?: number,
  ) => void;
  /** Hide the top toolbar (new chat, history, model selector) — set to false when used inside AppShell with sidebar */
  showToolbar?: boolean;
  /** Called when a new session is first created (so the sidebar can refresh its list) */
  onSessionCreated?: () => void;
}

/**
 * Main chat panel component — FluentUI v9 based.
 * Uses streaming responses with visible processing steps and model selection.
 */
export function ChatPanel(props: ChatPanelProps = {}): React.ReactElement {
  const styles = useStyles();
  const [messages, setMessages] = React.useState<ChatMessage[]>(() =>
    props.loadedSession?.messages?.length
      ? props.loadedSession.messages.map((m) => ({
          ...m,
          timestamp: new Date(),
        }))
      : [],
  );
  const [input, setInput] = React.useState("");
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();
  const [showHistory, setShowHistory] = React.useState(false);
  const [currentSessionId, setCurrentSessionId] = React.useState<
    string | undefined
  >(props.loadedSession?.sessionId);
  const [confirmedIndices, setConfirmedIndices] = React.useState<Set<number>>(
    new Set(),
  );

  // Streaming state
  const [streamingContent, setStreamingContent] = React.useState("");
  const [processingSteps, setProcessingSteps] = React.useState<
    ProcessingStep[]
  >([]);
  // Model selection
  const [models, setModels] = React.useState<ModelInfo[]>([]);
  const [selectedModelId, setSelectedModelId] = React.useState<string>("");

  // Project selector (fallback for standalone usage without AppShell)
  const [projects, setProjects] = React.useState<string[]>([]);
  const [selectedProject, setSelectedProject] = React.useState<string>(
    props.selectedProject ?? "",
  );

  // Attachments (new rich input)
  const [attachments, setAttachments] = React.useState<AttachmentItem[]>([]);

  const messagesEndRef = React.useRef<HTMLDivElement>(null);

  // Use project from props when provided (AppShell mode)
  React.useEffect(() => {
    if (props.selectedProject) {
      setSelectedProject(props.selectedProject);
    }
  }, [props.selectedProject]);

  // Load session from sidebar
  React.useEffect(() => {
    if (props.loadedSession) {
      setMessages(
        props.loadedSession.messages.map((m) => ({
          ...m,
          timestamp: new Date(),
        })),
      );
      setCurrentSessionId(props.loadedSession.sessionId);
    }
  }, [props.loadedSession]);

  React.useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, streamingContent, processingSteps]);

  // Load available models on mount
  React.useEffect(() => {
    fetchModels()
      .then((modelList) => {
        setModels(modelList);
        const defaultModel = modelList.find((m) => m.isDefault);
        if (defaultModel) {
          setSelectedModelId(defaultModel.id);
        } else if (modelList.length > 0) {
          setSelectedModelId(modelList[0].id);
        }
      })
      .catch(() => {
        // Can't reach backend yet — will retry or use default
      });
  }, []);

  // Load available projects and set default from context
  React.useEffect(() => {
    (async () => {
      try {
        const ctx = await getDevOpsContext();
        if (ctx.projectName) {
          setSelectedProject(ctx.projectName);
        }
        const projectList = await listProjects();
        setProjects(projectList);
        // If context project wasn't set, use first from list
        if (!ctx.projectName && projectList.length > 0) {
          setSelectedProject(projectList[0]);
        }
      } catch {
        // Fallback: use context project only
        try {
          const ctx = await getDevOpsContext();
          if (ctx.projectName) {
            setSelectedProject(ctx.projectName);
            setProjects([ctx.projectName]);
          }
        } catch {
          // silent
        }
      }
    })();
  }, []);

  // Auto-send initial message when panel opens with one
  const initialSent = React.useRef(false);
  const contextInjected = React.useRef(false);
  React.useEffect(() => {
    if (props.initialMessage && !initialSent.current) {
      initialSent.current = true;
      contextInjected.current = true; // context is included in the initial message
      // Small delay to let models load first
      const timer = setTimeout(() => {
        handleSend(props.initialMessage);
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [props.initialMessage]);

  const handleSend = React.useCallback(
    async (messageText?: string) => {
      const text = messageText ?? input.trim();
      if (!text || isLoading) return;

      // Build structured overrides for the backend (not visible in message text)
      const wiContext = props.workItemContext
        ? {
            id: props.workItemContext.id,
            title: props.workItemContext.title,
            type: props.workItemContext.type,
          }
        : undefined;

      // If user manually tagged work item IDs via attachments, mention them in the message
      let displayText = text;
      const wiAttachments = attachments.filter((a) => a.type === "workItem");
      if (wiAttachments.length > 0) {
        const idList = wiAttachments.map((a) => `#${a.id}`).join(", ");
        displayText = `[Work items: ${idList}]\n${text}`;
      }
      const memberAttachments = attachments.filter((a) => a.type === "member");
      if (memberAttachments.length > 0) {
        const names = memberAttachments.map((a) => a.label).join(", ");
        displayText = `[Team members: ${names}]\n${displayText}`;
      }

      // Build the API message — identical to displayText but with full file contents
      // appended as inline context blocks. The UI shows only displayText (clean).
      // Member attachments also include identity IDs so the AI can generate proper ADO mention HTML.
      let apiMessage = displayText;
      if (memberAttachments.length > 0) {
        // Replace the plain [Team members: Name] prefix with one that includes identity IDs
        const membersWithIds = memberAttachments
          .map((a) => `${a.label} (identity_id: ${a.id})`)
          .join(", ");
        apiMessage = apiMessage.replace(
          `[Team members: ${memberAttachments.map((a) => a.label).join(", ")}]`,
          `[Team members: ${membersWithIds}]`,
        );
      }
      const fileAttachments = attachments.filter(
        (a) => a.type === "file" && a.content,
      );
      if (fileAttachments.length > 0) {
        const fileBlocks = fileAttachments
          .map(
            (a) =>
              `<attached_file name="${a.label}">\n${a.content}\n</attached_file>`,
          )
          .join("\n\n");
        apiMessage = `${displayText}\n\n${fileBlocks}`;
      }

      setInput("");
      setError(undefined);
      setStreamingContent("");
      setProcessingSteps([]);

      const userMessage: ChatMessage = {
        role: "user",
        content: displayText,
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, userMessage]);
      setIsLoading(true);

      // Auto-create session on first message
      let sessionId = currentSessionId;
      if (!sessionId) {
        try {
          const session = await createSession(
            selectedProject || undefined,
            text.slice(0, 60),
          );
          sessionId = session.sessionId;
          setCurrentSessionId(sessionId);
          // Notify the sidebar so it can refresh its session list
          props.onSessionCreated?.();
        } catch (err) {
          console.warn("Session creation failed:", err);
        }
      }

      // Persist user message
      if (sessionId) {
        addMessage(sessionId, "user", displayText).catch((err) =>
          console.warn("Failed to persist user message:", err),
        );
      }

      let fullContent = "";
      let finalSuggestedActions: string[] | undefined;

      try {
        const history: ConversationMessage[] = messages.map((m) => ({
          role: m.role,
          content: m.content,
        }));

        await chatStream(
          apiMessage,
          (event: StreamEvent) => {
            switch (event.type) {
              case "step": {
                const stepId = `${event.step}-${Date.now()}`;
                const label =
                  event.content ??
                  (event.agent
                    ? `Running ${event.agent}...`
                    : event.tool
                      ? `Calling ${event.tool}...`
                      : "Processing...");

                // Mark previous active steps as done
                setProcessingSteps((prev) => {
                  const updated = prev.map((s) =>
                    s.status === "active"
                      ? { ...s, status: "done" as const }
                      : s,
                  );
                  return [
                    ...updated,
                    {
                      id: stepId,
                      label,
                      status: "active",
                      timestamp: new Date(),
                    },
                  ];
                });
                break;
              }
              case "content":
                if (event.content) {
                  fullContent += event.content;
                  setStreamingContent(fullContent);
                }
                break;
              case "content_replace":
                // Backend auto-intercepted a bad response — clear and restart
                fullContent = event.content ?? "";
                setStreamingContent(fullContent);
                break;
              case "suggestedActions":
                if (event.suggestedActions) {
                  finalSuggestedActions = event.suggestedActions;
                }
                break;
              case "error":
                throw new Error(event.content ?? "Streaming error");
              case "done":
                // Mark all steps as done
                setProcessingSteps((prev) =>
                  prev.map((s) => ({ ...s, status: "done" as const })),
                );
                break;
            }
          },
          history,
          selectedModelId || undefined,
          {
            projectName: selectedProject || undefined,
            workItemContext: wiContext,
          },
        );

        // Finalize the assistant message
        const assistantMessage: ChatMessage = {
          role: "assistant",
          content: fullContent || "I wasn't able to process your request.",
          suggestedActions: finalSuggestedActions,
          timestamp: new Date(),
        };
        setMessages((prev) => [...prev, assistantMessage]);
        setStreamingContent("");
        setProcessingSteps([]);

        // Persist assistant message
        if (sessionId && fullContent) {
          addMessage(sessionId, "assistant", fullContent).catch((err) =>
            console.warn("Failed to persist assistant message:", err),
          );
        }
      } catch (err) {
        const errorMessage =
          err instanceof Error ? err.message : "Unknown error occurred";
        setError(errorMessage);
        const errorAssistant: ChatMessage = {
          role: "assistant",
          content: `Sorry, I encountered an error: ${errorMessage}`,
          timestamp: new Date(),
        };
        setMessages((prev) => [...prev, errorAssistant]);
        setStreamingContent("");
        setProcessingSteps([]);
      } finally {
        setIsLoading(false);
      }
    },
    [
      input,
      isLoading,
      messages,
      currentSessionId,
      selectedModelId,
      selectedProject,
      attachments,
    ],
  );

  const handleSessionLoad = (
    sessionMessages: ConversationMessage[],
    sessionId: string,
  ) => {
    setMessages(
      sessionMessages.map((m) => ({
        ...m,
        timestamp: new Date(),
      })),
    );
    setCurrentSessionId(sessionId);
    setShowHistory(false);
  };

  const handleNewChat = () => {
    setMessages([]);
    setCurrentSessionId(undefined);
    setShowHistory(false);
    setConfirmedIndices(new Set());
    setStreamingContent("");
    setProcessingSteps([]);
    setAttachments([]);
  };

  const lastAssistantMessage = [...messages]
    .reverse()
    .find((m) => m.role === "assistant");
  const suggestedActions = lastAssistantMessage?.suggestedActions;

  const handleConfirm = (msgIdx: number) => {
    setConfirmedIndices((prev) => new Set(prev).add(msgIdx));
    handleSend("Yes, go ahead");
  };

  const handleReject = (msgIdx: number) => {
    setConfirmedIndices((prev) => new Set(prev).add(msgIdx));
    handleSend("No, cancel that");
  };

  return (
    <div className="chat-panel">
      {/* Session history panel */}
      {showHistory && (
        <SessionPanel
          onLoadSession={handleSessionLoad}
          onNewChat={handleNewChat}
          onClose={() => setShowHistory(false)}
          currentSessionId={currentSessionId}
        />
      )}

      {/* ─── Top toolbar (hidden when sidebar provides these controls) ─── */}
      {(props.showToolbar ?? true) && (
        <div className={styles.toolbarRow}>
          <Tooltip content="New chat" relationship="label">
            <Button
              appearance="subtle"
              icon={<AddRegular />}
              size="small"
              onClick={handleNewChat}
            >
              New Chat
            </Button>
          </Tooltip>
          <Tooltip content="Session history" relationship="label">
            <Button
              appearance={showHistory ? "primary" : "subtle"}
              icon={<HistoryRegular />}
              size="small"
              onClick={() => setShowHistory(!showHistory)}
            />
          </Tooltip>
          <div style={{ flexGrow: 1 }} />
          {models.length > 1 && (
            <>
              <SearchSparkleRegular fontSize={16} />
              <Dropdown
                className={styles.modelDropdown}
                size="small"
                value={
                  models.find((m) => m.id === selectedModelId)?.displayName ??
                  "Select model"
                }
                selectedOptions={[selectedModelId]}
                onOptionSelect={(_e, data) => {
                  if (data.optionValue) setSelectedModelId(data.optionValue);
                }}
                disabled={isLoading}
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
                          style={{ color: tokens.colorNeutralForeground3 }}
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
      )}

      {/* Work item context banner */}
      {props.workItemContext && (
        <div className={styles.contextBanner}>
          <DocumentRegular fontSize={20} className={styles.contextIcon} />
          <div className={styles.contextInfo}>
            <Text size={200} weight="semibold" className={styles.contextTitle}>
              #{props.workItemContext.id} {props.workItemContext.title}
            </Text>
            <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
              {props.workItemContext.type}
            </Text>
          </div>
        </div>
      )}

      {/* Messages area */}
      <div className="chat-messages">
        {messages.length === 0 &&
          !props.workItemContext &&
          props.loadedSession && (
            <div className={styles.welcomeContainer}>
              <BotSparkleRegular fontSize={40} className={styles.welcomeIcon} />
              <Text size={400} weight="semibold" block>
                No messages in this session
              </Text>
              <Text
                size={300}
                block
                style={{ color: tokens.colorNeutralForeground3 }}
              >
                This session has no saved messages. Start typing to continue.
              </Text>
            </div>
          )}
        {messages.length === 0 &&
          !props.workItemContext &&
          !props.loadedSession && (
            <div className={styles.welcomeContainer}>
              <BotSparkleRegular fontSize={40} className={styles.welcomeIcon} />
              <Text size={500} weight="semibold" block>
                Welcome to DevOps Copilot
              </Text>
              <Text
                size={300}
                block
                style={{ color: tokens.colorNeutralForeground3 }}
              >
                Your AI assistant for Azure DevOps. What would you like to do?
              </Text>
              <div className={styles.welcomeGrid}>
                {/* Product Owners */}
                <div className={styles.personaCard}>
                  <div className={styles.personaHeader}>
                    <ClipboardTaskRegular
                      fontSize={18}
                      className={styles.personaIconPo}
                    />
                    <Text size={200} weight="semibold">
                      Product Owners
                    </Text>
                  </div>
                  <div className={styles.personaChips}>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Show backlog health — items missing acceptance criteria, unestimated, or without descriptions",
                        )
                      }
                    >
                      Backlog health check
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Summarize current sprint progress — completed, in progress, new, and total story points",
                        )
                      }
                    >
                      Sprint progress summary
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Find user stories without acceptance criteria in the current sprint",
                        )
                      }
                    >
                      Stories missing AC
                    </Button>
                  </div>
                </div>
                {/* Developers */}
                <div className={styles.personaCard}>
                  <div className={styles.personaHeader}>
                    <CodeRegular
                      fontSize={18}
                      className={styles.personaIconDev}
                    />
                    <Text size={200} weight="semibold">
                      Developers
                    </Text>
                  </div>
                  <div className={styles.personaChips}>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Show my active work items sorted by priority",
                        )
                      }
                    >
                      My active work items
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "What items are blocked or have impediments?",
                        )
                      }
                    >
                      Blocked items
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Find bugs assigned to me that need attention",
                        )
                      }
                    >
                      My bugs needing attention
                    </Button>
                  </div>
                </div>
                {/* Testers */}
                <div className={styles.personaCard}>
                  <div className={styles.personaHeader}>
                    <BeakerRegular
                      fontSize={18}
                      className={styles.personaIconTest}
                    />
                    <Text size={200} weight="semibold">
                      Testers
                    </Text>
                  </div>
                  <div className={styles.personaChips}>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Find user stories in this sprint without test cases",
                        )
                      }
                    >
                      Stories without test cases
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Show recently resolved bugs that need verification testing",
                        )
                      }
                    >
                      Bugs needing verification
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Generate test cases for all active user stories in the current sprint",
                        )
                      }
                    >
                      Generate sprint test cases
                    </Button>
                  </div>
                </div>
                {/* Management */}
                <div className={styles.personaCard}>
                  <div className={styles.personaHeader}>
                    <DataBarVerticalRegular
                      fontSize={18}
                      className={styles.personaIconMgmt}
                    />
                    <Text size={200} weight="semibold">
                      Management
                    </Text>
                  </div>
                  <div className={styles.personaChips}>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Show items overdue or at risk in the current sprint",
                        )
                      }
                    >
                      At-risk items
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Summarize team workload distribution — who has the most items and story points",
                        )
                      }
                    >
                      Team workload overview
                    </Button>
                    <Button
                      appearance="subtle"
                      size="small"
                      className={styles.personaChip}
                      onClick={() =>
                        handleSend(
                          "Sprint health check — scope changes, blockers, and completion forecast",
                        )
                      }
                    >
                      Sprint health check
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          )}

        {/* Work-item-specific welcome — type-aware suggestions */}
        {messages.length === 0 &&
          props.workItemContext &&
          (() => {
            const wiType = props.workItemContext.type;
            const isStory = /user story|story/i.test(wiType);
            const isBug = /bug|issue|defect/i.test(wiType);
            const isFeatureEpic = /feature|epic/i.test(wiType);
            const isTask = /task/i.test(wiType);

            const typeIcon = isBug ? (
              <BugRegular fontSize={20} style={{ color: "#cc293d" }} />
            ) : isStory ? (
              <BookRegular fontSize={20} style={{ color: "#009ccc" }} />
            ) : isFeatureEpic ? (
              <TextBulletListSquareRegular
                fontSize={20}
                style={{ color: "#773b93" }}
              />
            ) : isTask ? (
              <TaskListSquareAddRegular
                fontSize={20}
                style={{ color: "#f2cb1d" }}
              />
            ) : (
              <DocumentRegular fontSize={20} style={{ color: "#0078d4" }} />
            );

            // Type-specific chips
            const typeChips: { label: string; prompt: string }[] = [];

            if (isStory) {
              typeChips.push(
                {
                  label: "Rate INVEST criteria",
                  prompt:
                    "Rate this user story against INVEST criteria (Independent, Negotiable, Valuable, Estimable, Small, Testable) and score each",
                },
                {
                  label: "Generate Given/When/Then AC",
                  prompt:
                    "Generate acceptance criteria for this user story in Given/When/Then format",
                },
                {
                  label: "Generate test cases",
                  prompt:
                    "Generate comprehensive test cases for this user story covering happy path, edge cases, and error scenarios",
                },
                {
                  label: "Suggest task breakdown",
                  prompt:
                    "Suggest a task breakdown for implementing this user story",
                },
                {
                  label: "Estimate complexity",
                  prompt:
                    "Estimate the story points and complexity for this user story based on its scope",
                },
                {
                  label: "What's missing?",
                  prompt:
                    "What's missing or could be improved in this user story?",
                },
              );
            } else if (isBug) {
              typeChips.push(
                {
                  label: "Analyze severity & impact",
                  prompt:
                    "Analyze the severity, impact, and affected areas of this bug",
                },
                {
                  label: "Suggest repro steps",
                  prompt:
                    "Suggest improvements to the reproduction steps for this bug",
                },
                {
                  label: "Find related/duplicate bugs",
                  prompt:
                    "Search for related or duplicate bugs similar to this one",
                },
                {
                  label: "Generate regression tests",
                  prompt:
                    "Generate regression test cases to verify this bug fix",
                },
                {
                  label: "Root cause analysis",
                  prompt: "Help analyze the potential root cause of this bug",
                },
                {
                  label: "What's missing?",
                  prompt:
                    "What's missing or could be improved in this bug report?",
                },
              );
            } else if (isFeatureEpic) {
              typeChips.push(
                {
                  label: "Suggest story breakdown",
                  prompt:
                    "Suggest user stories to break down this feature/epic",
                },
                {
                  label: "Estimate scope & effort",
                  prompt:
                    "Analyze the scope and estimate effort needed for this feature/epic",
                },
                {
                  label: "Identify risks & dependencies",
                  prompt:
                    "Identify risks and dependencies for this feature/epic",
                },
                {
                  label: "Generate feature test plan",
                  prompt:
                    "Generate a high-level test plan for this feature/epic",
                },
                {
                  label: "Analyze completeness",
                  prompt:
                    "Analyze the quality and completeness of this feature/epic requirements",
                },
                {
                  label: "What's missing?",
                  prompt:
                    "What's missing or could be improved in this feature/epic?",
                },
              );
            } else if (isTask) {
              typeChips.push(
                {
                  label: "Clarify requirements",
                  prompt:
                    "Clarify the requirements and acceptance criteria for this task",
                },
                {
                  label: "Estimate remaining effort",
                  prompt: "Help estimate the remaining effort for this task",
                },
                {
                  label: "Find dependencies",
                  prompt:
                    "Find blocking dependencies or related items for this task",
                },
                {
                  label: "What's missing?",
                  prompt: "What's missing or could be improved in this task?",
                },
              );
            } else {
              typeChips.push(
                {
                  label: "Analyze completeness",
                  prompt:
                    "Analyze the quality and completeness of this work item",
                },
                {
                  label: "Generate test cases",
                  prompt: "Generate test cases for this work item",
                },
                {
                  label: "What's missing?",
                  prompt:
                    "What's missing or could be improved in this work item?",
                },
                {
                  label: "Suggest child items",
                  prompt: "Suggest child items or sub-tasks for this work item",
                },
              );
            }

            return (
              <div
                className={styles.welcomeContainer}
                style={{ paddingTop: "10vh" }}
              >
                <div className={styles.wiWelcomeHeader}>
                  {typeIcon}
                  <div style={{ minWidth: 0 }}>
                    <Text size={300} weight="semibold" block truncate>
                      #{props.workItemContext!.id}{" "}
                      {props.workItemContext!.title}
                    </Text>
                    <Text
                      size={100}
                      style={{ color: tokens.colorNeutralForeground3 }}
                    >
                      {wiType}
                    </Text>
                  </div>
                </div>
                <Text
                  size={200}
                  block
                  style={{
                    color: tokens.colorNeutralForeground3,
                    marginBottom: 12,
                  }}
                >
                  Quick actions for this {wiType.toLowerCase()}:
                </Text>
                <div className={styles.wiChipGrid}>
                  {typeChips.map((chip, i) => (
                    <Button
                      key={i}
                      appearance="outline"
                      size="small"
                      className={styles.personaChip}
                      onClick={() => handleSend(chip.prompt)}
                    >
                      {chip.label}
                    </Button>
                  ))}
                </div>
              </div>
            );
          })()}

        {messages.map((msg, idx) => (
          <div key={idx} className={`message-container message-${msg.role}`}>
            <MessageBubble
              role={msg.role}
              content={stripActionsLine(msg.content)}
              timestamp={msg.timestamp}
            />
            {msg.workItems && msg.workItems.length > 0 && (
              <div className="work-items-grid">
                {msg.workItems.map((wi) => (
                  <WorkItemCard
                    key={wi.id}
                    workItem={wi}
                    onPreview={
                      props.onOpenPreview
                        ? (item) =>
                            props.onOpenPreview!(
                              "workItem",
                              mapWorkItemToFormData(item),
                              item.id,
                            )
                        : undefined
                    }
                  />
                ))}
              </div>
            )}
            {/* Human-in-the-loop: action buttons for write confirmations */}
            {msg.role === "assistant" &&
              isConfirmationPrompt(msg.content) &&
              !confirmedIndices.has(idx) &&
              !isLoading && (
                <div className={styles.actionButtons}>
                  <Button
                    appearance="primary"
                    size="small"
                    icon={<CheckmarkRegular />}
                    onClick={() => handleConfirm(idx)}
                  >
                    Confirm
                  </Button>
                  <Button
                    appearance="outline"
                    size="small"
                    icon={<EditRegular />}
                    onClick={() => {
                      setConfirmedIndices((prev) => new Set(prev).add(idx));
                    }}
                  >
                    Edit
                  </Button>
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<DismissCircleRegular />}
                    onClick={() => handleReject(idx)}
                  >
                    Cancel
                  </Button>
                </div>
              )}
            {/* Inline selectable options — shown when the AI presents a numbered list to pick from */}
            {msg.role === "assistant" &&
              idx === messages.length - 1 &&
              !isLoading &&
              !isConfirmationPrompt(msg.content) &&
              (() => {
                const options = extractInlineOptions(msg.content);
                if (!options) return null;
                return (
                  <div className={styles.optionButtons}>
                    {options.map((opt, i) => (
                      <Button
                        key={i}
                        appearance="outline"
                        size="small"
                        onClick={() => handleSend(opt)}
                      >
                        {opt}
                      </Button>
                    ))}
                  </div>
                );
              })()}
          </div>
        ))}

        {/* Streaming: processing steps + live content */}
        {isLoading && (
          <div className="message-container message-assistant">
            <ThinkingIndicator steps={processingSteps} isActive={isLoading} />
            {streamingContent ? (
              <div className={styles.streamingBubble}>
                <div
                  className="bubble-content md-content"
                  dangerouslySetInnerHTML={{
                    __html: stripActionsLine(streamingContent),
                  }}
                />
                <span className="streaming-cursor">|</span>
              </div>
            ) : (
              processingSteps.length === 0 && (
                <Spinner size="tiny" label="Connecting..." />
              )
            )}
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Suggested actions */}
      {suggestedActions && suggestedActions.length > 0 && !isLoading && (
        <div className={styles.suggestionChips}>
          {suggestedActions.map((action, idx) => (
            <Button
              key={idx}
              appearance="outline"
              size="small"
              onClick={() => handleSend(action)}
            >
              {action}
            </Button>
          ))}
        </div>
      )}

      {/* Error banner */}
      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
          <MessageBarActions>
            <Button
              appearance="transparent"
              icon={<DismissRegular />}
              onClick={() => setError(undefined)}
              size="small"
            />
          </MessageBarActions>
        </MessageBar>
      )}

      <ChatInputBar
        value={input}
        onChange={setInput}
        onSend={() => handleSend()}
        disabled={isLoading}
        placeholder={
          props.workItemContext
            ? `Ask about #${props.workItemContext.id}... (Enter to send)`
            : "Ask about your work items... (Enter to send, Shift+Enter for new line)"
        }
        models={models}
        selectedModelId={selectedModelId}
        onModelChange={setSelectedModelId}
        attachments={attachments}
        onAddAttachment={(item) => setAttachments((prev) => [...prev, item])}
        onRemoveAttachment={(id) =>
          setAttachments((prev) => prev.filter((a) => a.id !== id))
        }
        searchWorkItems={async (query) => {
          return apiSearchWorkItems(query, selectedProject);
        }}
        searchMembers={async (query) => {
          return apiSearchMembers(query, selectedProject);
        }}
        searchRepos={async (query) => {
          return apiSearchRepositories(query, selectedProject);
        }}
      />
    </div>
  );
}
