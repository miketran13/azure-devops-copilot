import * as SDK from "azure-devops-extension-sdk";
import * as React from "react";
import { createRoot } from "react-dom/client";
import {
  IWorkItemFormService,
  WorkItemTrackingServiceIds,
} from "azure-devops-extension-api/WorkItemTracking";
import {
  Button,
  Spinner,
  Textarea,
  Text,
  Dropdown,
  Option,
  makeStyles,
  tokens,
  shorthands,
  Tooltip,
} from "@fluentui/react-components";
import {
  SearchRegular,
  BeakerRegular,
  CheckmarkCircleRegular,
  ListRegular,
  ChatRegular,
  SendRegular,
  CheckmarkRegular,
  DismissCircleRegular,
  ArrowSyncRegular,
  SearchSparkleRegular,
  PanelRightRegular,
  HistoryRegular,
  ArrowForwardRegular,
  AddRegular,
  BotSparkleRegular,
} from "@fluentui/react-icons";
import { FluentThemeProvider } from "../providers/FluentThemeProvider";
import {
  analyzeWorkItem,
  generateTestCases,
  suggestChildItems,
  generateAcceptanceCriteria,
  chatStream,
  fetchModels,
} from "../services/backendApi";
import {
  createSession,
  addMessage,
  listSessions,
  getSession,
} from "../services/sessionApi";
import { getDevOpsContext } from "../services/devopsContext";
import { openChatPanel } from "../utils/openChatPanel";
import { formatMarkdown } from "../utils/formatMarkdown";
import { MessageBubble } from "../components/MessageBubble";
import { SessionPanel } from "../components/SessionPanel";
import type {
  ConversationMessage,
  StreamEvent,
  ProcessingStep,
  ModelInfo,
  SessionInfo,
} from "../models/types";
import "../styles/markdown.scss";
import "../components/MessageBubble.scss";
import "./WorkItemGroup.scss";
import {
  isConfirmationPrompt,
  stripActionsLine,
  extractInlineOptions,
} from "../utils/chatParsing";

interface ChatMsg {
  role: "user" | "assistant";
  content: string;
  suggestedActions?: string[];
  timestamp: Date;
}

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    height: "100%",
    minHeight: "500px",
    ...shorthands.padding("8px"),
    ...shorthands.gap("8px"),
  },
  actions: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("6px"),
    flexShrink: 0,
  },
  chatContainer: {
    flexGrow: 1,
    display: "flex",
    flexDirection: "column",
    ...shorthands.overflow("hidden"),
    minHeight: 0,
  },
  chatMessages: {
    flexGrow: 1,
    overflowY: "auto",
    ...shorthands.padding("8px"),
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("8px"),
  },
  chatInputArea: {
    display: "flex",
    alignItems: "flex-end",
    ...shorthands.gap("6px"),
    ...shorthands.padding("8px"),
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke2),
    flexShrink: 0,
  },
  loading: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    height: "100%",
  },
  empty: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    height: "100%",
    color: tokens.colorNeutralForeground3,
  },
  processingSteps: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("4px"),
    ...shorthands.padding("8px", "12px"),
    ...shorthands.borderRadius("8px"),
    backgroundColor: tokens.colorNeutralBackground3,
    fontSize: "12px",
    maxWidth: "90%",
  },
  stepItemActive: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("6px"),
    color: tokens.colorBrandForeground1,
    fontWeight: 600 as unknown as string,
  },
  stepItemDone: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("6px"),
    color: tokens.colorNeutralForeground3,
    opacity: 0.7,
  },
  streamingBubble: {
    ...shorthands.padding("10px", "14px"),
    ...shorthands.borderRadius("12px"),
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground1,
    borderBottomLeftRadius: "4px",
    maxWidth: "100%",
    wordBreak: "break-word",
    fontSize: "13px",
    lineHeight: "1.5",
  },
  toolbarRow: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
    ...shorthands.padding("4px", "8px"),
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke2),
    flexShrink: 0,
  },
  modelDropdown: {
    minWidth: "140px",
    maxWidth: "200px",
  },
  actionButtons: {
    display: "flex",
    ...shorthands.gap("6px"),
    ...shorthands.padding("4px", "0"),
    marginTop: "4px",
  },
  suggestionChips: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("6px"),
    ...shorthands.padding("4px", "8px"),
    flexShrink: 0,
  },
  resumeBanner: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
    ...shorthands.padding("10px", "12px"),
    ...shorthands.borderRadius("8px"),
    backgroundColor: tokens.colorNeutralBackground3,
    ...shorthands.margin("0", "0", "4px"),
  },
  resumeText: {
    flexGrow: 1,
    minWidth: 0,
  },
  historyContainer: {
    position: "relative" as const,
    flexGrow: 1,
    display: "flex",
    flexDirection: "column",
    ...shorthands.overflow("hidden"),
    minHeight: 0,
  },
  emptyHint: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    flexGrow: 1,
    ...shorthands.gap("8px"),
    ...shorthands.padding("24px"),
    textAlign: "center" as const,
    color: tokens.colorNeutralForeground3,
  },
  emptyIcon: {
    color: tokens.colorBrandForeground1,
    opacity: 0.5,
  },
});

/**
 * Work Item Form Group — AI panel displayed on every work item form.
 * Full streaming chat experience with processing steps and model selection.
 */
function WorkItemGroupContent(): React.ReactElement {
  const styles = useStyles();
  const [ready, setReady] = React.useState(false);
  const [workItemId, setWorkItemId] = React.useState<number | undefined>();
  const [workItemType, setWorkItemType] = React.useState<string>("");
  const [workItemTitle, setWorkItemTitle] = React.useState<string>("");

  const [isLoading, setIsLoading] = React.useState(false);
  const [activeAction, setActiveAction] = React.useState<string>("");

  const [showChat, setShowChat] = React.useState(false);
  const [chatMessages, setChatMessages] = React.useState<ChatMsg[]>([]);
  const [chatInput, setChatInput] = React.useState("");
  const [confirmedMsgIndices, setConfirmedMsgIndices] = React.useState<
    Set<number>
  >(new Set());

  // Streaming state
  const [streamingContent, setStreamingContent] = React.useState("");
  const [processingSteps, setProcessingSteps] = React.useState<
    ProcessingStep[]
  >([]);

  // Model selection
  const [models, setModels] = React.useState<ModelInfo[]>([]);
  const [selectedModelId, setSelectedModelId] = React.useState<string>("");

  // Session persistence
  const [currentSessionId, setCurrentSessionId] = React.useState<
    string | undefined
  >();
  const [showHistory, setShowHistory] = React.useState(false);
  const [hasExistingSession, setHasExistingSession] = React.useState(false);
  const [resumeDismissed, setResumeDismissed] = React.useState(false);
  const latestSessionRef = React.useRef<SessionInfo | undefined>();

  const chatEndRef = React.useRef<HTMLDivElement>(null);
  const chatInputRef = React.useRef<HTMLTextAreaElement>(null);

  React.useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [chatMessages, streamingContent, processingSteps]);

  React.useEffect(() => {
    SDK.init().then(() => {
      SDK.register(SDK.getContributionId(), () => ({}));
      SDK.ready().then(async () => {
        setReady(true);
        try {
          const formService = await SDK.getService<IWorkItemFormService>(
            WorkItemTrackingServiceIds.WorkItemFormService,
          );
          const id = await formService.getId();
          const fields = await formService.getFieldValues([
            "System.WorkItemType",
            "System.Title",
          ]);
          setWorkItemId(id);
          setWorkItemType((fields["System.WorkItemType"] as string) ?? "");
          setWorkItemTitle((fields["System.Title"] as string) ?? "");
        } catch {
          // Work item might not be saved yet
        }
      });
    });
  }, []);

  // Load available models on mount
  React.useEffect(() => {
    fetchModels()
      .then((modelList) => {
        setModels(modelList);
        const defaultModel = modelList.find((m) => m.isDefault);
        if (defaultModel) setSelectedModelId(defaultModel.id);
        else if (modelList.length > 0) setSelectedModelId(modelList[0].id);
      })
      .catch(() => {});
  }, []);

  // Check for existing sessions for this work item
  React.useEffect(() => {
    if (!workItemId) return;
    const tag = `#${workItemId}`;
    getDevOpsContext()
      .then((ctx) => listSessions(ctx.projectName))
      .then((allSessions) => {
        const matching = allSessions.filter((s) =>
          (s.title || "").includes(tag),
        );
        if (matching.length > 0) {
          setHasExistingSession(true);
          latestSessionRef.current = matching[0]; // most recent first
        }
      })
      .catch(() => {});
  }, [workItemId]);

  /** Streaming chat send — used by both free chat and quick actions */
  const sendStreamingMessage = React.useCallback(
    async (text: string) => {
      if (!text || isLoading || !workItemId) return;

      setChatInput("");
      setStreamingContent("");
      setProcessingSteps([]);

      const userMsg: ChatMsg = {
        role: "user",
        content: text,
        timestamp: new Date(),
      };
      setChatMessages((prev) => [...prev, userMsg]);
      setIsLoading(true);
      setShowChat(true);

      // Auto-create session on first message
      let sessionId = currentSessionId;
      if (!sessionId) {
        try {
          const ctx = await getDevOpsContext();
          const title = `#${workItemId} - ${workItemTitle}`.slice(0, 60);
          const session = await createSession(ctx.projectName, title);
          sessionId = session.sessionId;
          setCurrentSessionId(sessionId);
        } catch {
          // Session creation failed — continue without persistence
        }
      }

      // Persist user message
      if (sessionId) {
        addMessage(sessionId, "user", text).catch(() => {});
      }

      let fullContent = "";
      let finalSuggestedActions: string[] | undefined;

      try {
        const history: ConversationMessage[] = chatMessages.map((m) => ({
          role: m.role,
          content: m.content,
        }));

        await chatStream(
          text,
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
              case "suggestedActions":
                if (event.suggestedActions) {
                  finalSuggestedActions = event.suggestedActions;
                }
                break;
              case "error":
                throw new Error(event.content ?? "Streaming error");
              case "done":
                setProcessingSteps((prev) =>
                  prev.map((s) => ({ ...s, status: "done" as const })),
                );
                break;
            }
          },
          history,
          selectedModelId || undefined,
          {
            workItemContext: workItemId
              ? { id: workItemId, title: workItemTitle, type: workItemType }
              : undefined,
          },
        );

        const assistantMsg: ChatMsg = {
          role: "assistant",
          content: fullContent || "I wasn't able to process your request.",
          suggestedActions: finalSuggestedActions,
          timestamp: new Date(),
        };
        setChatMessages((prev) => [...prev, assistantMsg]);
        setStreamingContent("");
        setProcessingSteps([]);

        // Persist assistant message
        if (sessionId && fullContent) {
          addMessage(sessionId, "assistant", fullContent).catch(() => {});
        }
      } catch (err) {
        const msg = err instanceof Error ? err.message : "Unknown error";
        setChatMessages((prev) => [
          ...prev,
          {
            role: "assistant",
            content: `Sorry, I encountered an error: ${msg}`,
            timestamp: new Date(),
          },
        ]);
        setStreamingContent("");
        setProcessingSteps([]);
      } finally {
        setIsLoading(false);
        chatInputRef.current?.focus();
      }
    },
    [
      isLoading,
      workItemId,
      workItemTitle,
      workItemType,
      chatMessages,
      selectedModelId,
      currentSessionId,
    ],
  );

  /** Resume a previous session for this work item */
  const handleResumeSession = React.useCallback(async () => {
    const session = latestSessionRef.current;
    if (!session) return;
    try {
      const full = await getSession(session.sessionId);
      const msgs: ChatMsg[] = (full.messages ?? []).map((m) => ({
        role: m.role,
        content: m.content,
        timestamp: new Date(),
      }));
      setChatMessages(msgs);
      setCurrentSessionId(session.sessionId);
      setShowChat(true);
      setResumeDismissed(true);
    } catch {
      // Failed to load — user can start fresh
      setResumeDismissed(true);
    }
  }, []);

  /** Load a session from the history panel */
  const handleLoadSession = React.useCallback(
    (messages: ConversationMessage[], sessionId: string) => {
      const msgs: ChatMsg[] = messages.map((m) => ({
        role: m.role,
        content: m.content,
        timestamp: new Date(),
      }));
      setChatMessages(msgs);
      setCurrentSessionId(sessionId);
      setShowChat(true);
      setShowHistory(false);
      setResumeDismissed(true);
    },
    [],
  );

  /** Start a new chat (clearing current session) */
  const handleNewChat = React.useCallback(() => {
    setChatMessages([]);
    setCurrentSessionId(undefined);
    setConfirmedMsgIndices(new Set());
    setStreamingContent("");
    setProcessingSteps([]);
    setShowHistory(false);
    setResumeDismissed(true);
  }, []);

  /** Quick actions (Analyze, Tests, etc.) use streaming too */
  const handleAction = async (
    action: string,
    apiCall: (id: number) => Promise<{ reply: string }>,
  ) => {
    if (!workItemId) return;
    setActiveAction(action);

    const actionLabels: Record<string, string> = {
      analyze: "Analyze the quality of this work item",
      testcases: "Generate test cases for this work item",
      acceptance: "Generate acceptance criteria for this work item",
      children: "Suggest child items for this work item",
    };
    await sendStreamingMessage(actionLabels[action] ?? action);
  };

  const handleChatSend = React.useCallback(async () => {
    const text = chatInput.trim();
    if (!text) return;
    await sendStreamingMessage(text);
  }, [chatInput, sendStreamingMessage]);

  const handleChatKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleChatSend();
    }
  };

  /** Handle confirm/cancel action button clicks */
  const handleConfirmAction = React.useCallback(
    (msgIdx: number, text: string) => {
      setConfirmedMsgIndices((prev) => new Set(prev).add(msgIdx));
      sendStreamingMessage(text);
    },
    [sendStreamingMessage],
  );

  const openChat = () => {
    setShowChat(true);
    setActiveAction("chat");
    setTimeout(() => chatInputRef.current?.focus(), 100);
  };

  /** Open the full chat sidebar panel (CopilotKit Sidebar pattern via ADO panel API) */
  const openSidebar = (initialMessage?: string) => {
    openChatPanel({
      initialMessage,
      workItemContext: workItemId
        ? { id: workItemId, title: workItemTitle, type: workItemType }
        : undefined,
    });
  };

  /** Render a processing step item with appropriate icon */
  const renderStep = (step: ProcessingStep) => {
    const className =
      step.status === "active" ? styles.stepItemActive : styles.stepItemDone;
    return (
      <div key={step.id} className={className}>
        {step.status === "active" ? (
          <ArrowSyncRegular fontSize={12} className="spin-icon" />
        ) : (
          <CheckmarkCircleRegular fontSize={12} />
        )}
        <span>{step.label}</span>
      </div>
    );
  };

  // Suggested actions from last assistant message
  const lastAssistantMsg = [...chatMessages]
    .reverse()
    .find((m) => m.role === "assistant");
  const suggestedActions = lastAssistantMsg?.suggestedActions;

  if (!ready) {
    return (
      <div className={styles.loading}>
        <Spinner size="tiny" label="Loading Copilot..." />
      </div>
    );
  }

  if (!workItemId) {
    return (
      <div className={styles.empty}>
        <Text size={200}>Save the work item first to use AI analysis.</Text>
      </div>
    );
  }

  const isFeatureOrEpic = ["Feature", "Epic"].includes(workItemType);

  return (
    <div className={styles.container}>
      {/* Quick action buttons */}
      <div className={styles.actions}>
        <Tooltip content="Analyze quality" relationship="label">
          <Button
            appearance={activeAction === "analyze" ? "primary" : "outline"}
            icon={<SearchRegular />}
            size="small"
            onClick={() => handleAction("analyze", analyzeWorkItem)}
            disabled={isLoading}
          >
            Analyze
          </Button>
        </Tooltip>
        <Tooltip content="Generate test cases" relationship="label">
          <Button
            appearance={activeAction === "testcases" ? "primary" : "outline"}
            icon={<BeakerRegular />}
            size="small"
            onClick={() => handleAction("testcases", generateTestCases)}
            disabled={isLoading}
          >
            Tests
          </Button>
        </Tooltip>
        <Tooltip content="Generate acceptance criteria" relationship="label">
          <Button
            appearance={activeAction === "acceptance" ? "primary" : "outline"}
            icon={<CheckmarkCircleRegular />}
            size="small"
            onClick={() =>
              handleAction("acceptance", generateAcceptanceCriteria)
            }
            disabled={isLoading}
          >
            Criteria
          </Button>
        </Tooltip>
        {isFeatureOrEpic && (
          <Tooltip content="Suggest child items" relationship="label">
            <Button
              appearance={activeAction === "children" ? "primary" : "outline"}
              icon={<ListRegular />}
              size="small"
              onClick={() => handleAction("children", suggestChildItems)}
              disabled={isLoading}
            >
              Children
            </Button>
          </Tooltip>
        )}
        <Tooltip content="Chat about this item" relationship="label">
          <Button
            appearance={activeAction === "chat" ? "primary" : "outline"}
            icon={<ChatRegular />}
            size="small"
            onClick={openChat}
            disabled={isLoading}
          >
            Chat
          </Button>
        </Tooltip>
        <Tooltip content="Session history" relationship="label">
          <Button
            appearance={showHistory ? "primary" : "outline"}
            icon={<HistoryRegular />}
            size="small"
            onClick={() => setShowHistory(!showHistory)}
            disabled={isLoading}
          >
            History
          </Button>
        </Tooltip>
        <Tooltip content="Open full chat sidebar" relationship="label">
          <Button
            appearance="subtle"
            icon={<PanelRightRegular />}
            size="small"
            onClick={() => openSidebar()}
            disabled={isLoading}
          />
        </Tooltip>
      </div>

      {/* Resume previous session banner */}
      {hasExistingSession && !resumeDismissed && !showChat && (
        <div className={styles.resumeBanner}>
          <ChatRegular fontSize={16} />
          <div className={styles.resumeText}>
            <Text size={200} weight="semibold" block>
              Previous conversation found
            </Text>
            <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
              Continue where you left off?
            </Text>
          </div>
          <Button
            appearance="primary"
            size="small"
            icon={<ArrowForwardRegular />}
            onClick={handleResumeSession}
          >
            Resume
          </Button>
          <Button
            appearance="subtle"
            size="small"
            icon={<AddRegular />}
            onClick={() => {
              setResumeDismissed(true);
              openChat();
            }}
          >
            New
          </Button>
        </div>
      )}

      {/* Empty state — when no action selected and no resume banner */}
      {!showChat && (hasExistingSession ? resumeDismissed : true) && (
        <div className={styles.emptyHint}>
          <BotSparkleRegular fontSize={32} className={styles.emptyIcon} />
          <Text size={200}>
            Select an action above or click <strong>Chat</strong> to analyze
            this {workItemType.toLowerCase() || "work item"}
          </Text>
        </div>
      )}

      {/* Chat area — always visible once activated */}
      {showChat && (
        <div className={styles.historyContainer}>
          {/* Session history panel overlay */}
          {showHistory && (
            <SessionPanel
              onLoadSession={handleLoadSession}
              onNewChat={handleNewChat}
              onClose={() => setShowHistory(false)}
              currentSessionId={currentSessionId}
              filterFn={
                workItemId
                  ? (s) => (s.title || "").includes(`#${workItemId}`)
                  : undefined
              }
            />
          )}
          <div className={styles.chatContainer}>
            <div className={styles.chatMessages}>
              {chatMessages.length === 0 && (
                <Text
                  size={200}
                  style={{ color: tokens.colorNeutralForeground3, padding: 8 }}
                >
                  Ask anything about <strong>#{workItemId}</strong> — e.g.
                  &quot;What&apos;s missing in this story?&quot;
                </Text>
              )}
              {chatMessages.map((msg, idx) => (
                <div key={idx}>
                  <MessageBubble
                    role={msg.role}
                    content={stripActionsLine(msg.content)}
                    timestamp={msg.timestamp}
                  />
                  {/* Action buttons for assistant confirmation prompts */}
                  {msg.role === "assistant" &&
                    !confirmedMsgIndices.has(idx) &&
                    !isLoading &&
                    isConfirmationPrompt(msg.content) && (
                      <div className={styles.actionButtons}>
                        <Button
                          appearance="primary"
                          size="small"
                          icon={<CheckmarkRegular />}
                          onClick={() =>
                            handleConfirmAction(idx, "Yes, go ahead")
                          }
                        >
                          Confirm
                        </Button>
                        <Button
                          appearance="subtle"
                          size="small"
                          icon={<DismissCircleRegular />}
                          onClick={() =>
                            handleConfirmAction(idx, "No, cancel that")
                          }
                        >
                          Cancel
                        </Button>
                      </div>
                    )}
                </div>
              ))}

              {/* Streaming: processing steps + live content */}
              {isLoading && (
                <div>
                  {processingSteps.length > 0 && (
                    <div className={styles.processingSteps}>
                      {processingSteps.map(renderStep)}
                    </div>
                  )}
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

              <div ref={chatEndRef} />
            </div>

            {/* Suggested actions */}
            {suggestedActions && suggestedActions.length > 0 && !isLoading && (
              <div className={styles.suggestionChips}>
                {suggestedActions.map((action, idx) => (
                  <Button
                    key={idx}
                    appearance="outline"
                    size="small"
                    onClick={() => sendStreamingMessage(action)}
                  >
                    {action}
                  </Button>
                ))}
              </div>
            )}

            {/* Model selector */}
            {models.length > 1 && (
              <div className={styles.toolbarRow}>
                <SearchSparkleRegular fontSize={14} />
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
              </div>
            )}

            {/* Input area */}
            <div className={styles.chatInputArea}>
              <Textarea
                ref={chatInputRef}
                style={{ flexGrow: 1 }}
                value={chatInput}
                onChange={(_e, data) => setChatInput(data.value)}
                onKeyDown={handleChatKeyDown}
                placeholder={`Ask about #${workItemId}... (Enter to send)`}
                rows={2}
                resize="vertical"
                disabled={isLoading}
              />
              <Tooltip content="Send" relationship="label">
                <Button
                  appearance="primary"
                  icon={<SendRegular />}
                  size="small"
                  onClick={handleChatSend}
                  disabled={!chatInput.trim() || isLoading}
                />
              </Tooltip>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function WorkItemGroup(): React.ReactElement {
  return (
    <FluentThemeProvider>
      <WorkItemGroupContent />
    </FluentThemeProvider>
  );
}

const root = createRoot(document.getElementById("root")!);
root.render(<WorkItemGroup />);
