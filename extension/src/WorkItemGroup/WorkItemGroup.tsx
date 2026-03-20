import * as SDK from "azure-devops-extension-sdk";
import * as React from "react";
import * as ReactDOM from "react-dom";
import {
  IWorkItemFormService,
  WorkItemTrackingServiceIds,
} from "azure-devops-extension-api/WorkItemTracking";
import {
  analyzeWorkItem,
  generateTestCases,
  suggestChildItems,
  generateAcceptanceCriteria,
  chat,
} from "../services/backendApi";
import { formatMarkdown } from "../utils/formatMarkdown";
import type { ConversationMessage } from "../models/types";
import "../styles/markdown.scss";
import "./WorkItemGroup.scss";

interface ChatMsg {
  role: "user" | "assistant";
  content: string;
  timestamp: Date;
}

/**
 * Work Item Form Group — AI panel displayed on every work item form.
 * Provides quick AI actions + inline contextual chat for the current work item.
 */
function WorkItemGroup(): React.ReactElement {
  const [ready, setReady] = React.useState(false);
  const [workItemId, setWorkItemId] = React.useState<number | undefined>();
  const [workItemType, setWorkItemType] = React.useState<string>("");
  const [workItemTitle, setWorkItemTitle] = React.useState<string>("");

  // Quick action state
  const [result, setResult] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState(false);
  const [activeAction, setActiveAction] = React.useState<string>("");

  // Inline chat state
  const [showChat, setShowChat] = React.useState(false);
  const [chatMessages, setChatMessages] = React.useState<ChatMsg[]>([]);
  const [chatInput, setChatInput] = React.useState("");
  const [isChatLoading, setIsChatLoading] = React.useState(false);
  const chatEndRef = React.useRef<HTMLDivElement>(null);
  const chatInputRef = React.useRef<HTMLTextAreaElement>(null);

  React.useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [chatMessages]);

  React.useEffect(() => {
    SDK.init().then(() => {
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

  // ─── Quick action handler ───
  const handleAction = async (
    action: string,
    apiCall: (id: number) => Promise<{ reply: string }>,
  ) => {
    if (!workItemId) return;
    setActiveAction(action);
    setIsLoading(true);
    setResult("");
    setShowChat(false); // Switch to result view

    try {
      const response = await apiCall(workItemId);
      setResult(response.reply);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Unknown error";
      setResult(`Error: ${msg}`);
    } finally {
      setIsLoading(false);
    }
  };

  // ─── Chat handler ───
  const handleChatSend = React.useCallback(async () => {
    const text = chatInput.trim();
    if (!text || isChatLoading || !workItemId) return;

    setChatInput("");
    const userMsg: ChatMsg = {
      role: "user",
      content: text,
      timestamp: new Date(),
    };
    setChatMessages((prev) => [...prev, userMsg]);
    setIsChatLoading(true);

    try {
      // Build history for multi-turn
      const history: ConversationMessage[] = chatMessages.map((m) => ({
        role: m.role,
        content: m.content,
      }));

      // Prefix message with work item context so the agent knows which item
      const contextPrefix = `[Context: This conversation is about work item #${workItemId} "${workItemTitle}" (${workItemType})]\n`;
      const fullMessage = history.length === 0 ? contextPrefix + text : text;

      const response = await chat(fullMessage, history);

      setChatMessages((prev) => [
        ...prev,
        { role: "assistant", content: response.reply, timestamp: new Date() },
      ]);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Unknown error";
      setChatMessages((prev) => [
        ...prev,
        { role: "assistant", content: `Error: ${msg}`, timestamp: new Date() },
      ]);
    } finally {
      setIsChatLoading(false);
      chatInputRef.current?.focus();
    }
  }, [
    chatInput,
    isChatLoading,
    workItemId,
    workItemTitle,
    workItemType,
    chatMessages,
  ]);

  const handleChatKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleChatSend();
    }
  };

  const openChat = () => {
    setShowChat(true);
    setResult("");
    setActiveAction("chat");
    setTimeout(() => chatInputRef.current?.focus(), 100);
  };

  if (!ready) {
    return <div className="wig-loading">Loading Copilot...</div>;
  }

  if (!workItemId) {
    return (
      <div className="wig-empty">
        <p>Save the work item first to use AI analysis.</p>
      </div>
    );
  }

  const isFeatureOrEpic = ["Feature", "Epic"].includes(workItemType);

  return (
    <div className="wig-container">
      {/* ─── Quick action buttons ─── */}
      <div className="wig-actions">
        <button
          className={`wig-action-btn ${activeAction === "analyze" ? "active" : ""}`}
          onClick={() => handleAction("analyze", analyzeWorkItem)}
          disabled={isLoading || isChatLoading}
        >
          🔍 Analyze Quality
        </button>
        <button
          className={`wig-action-btn ${activeAction === "testcases" ? "active" : ""}`}
          onClick={() => handleAction("testcases", generateTestCases)}
          disabled={isLoading || isChatLoading}
        >
          🧪 Test Cases
        </button>
        <button
          className={`wig-action-btn ${activeAction === "acceptance" ? "active" : ""}`}
          onClick={() => handleAction("acceptance", generateAcceptanceCriteria)}
          disabled={isLoading || isChatLoading}
        >
          ✅ Acceptance Criteria
        </button>
        {isFeatureOrEpic && (
          <button
            className={`wig-action-btn ${activeAction === "children" ? "active" : ""}`}
            onClick={() => handleAction("children", suggestChildItems)}
            disabled={isLoading || isChatLoading}
          >
            📋 Child Items
          </button>
        )}
        <button
          className={`wig-action-btn wig-chat-btn ${activeAction === "chat" ? "active" : ""}`}
          onClick={openChat}
          disabled={isLoading || isChatLoading}
        >
          💬 Chat
        </button>
      </div>

      {/* ─── Loading indicator ─── */}
      {isLoading && (
        <div className="wig-loading-inline">
          <div className="spinner-small" />
          <span>Analyzing...</span>
        </div>
      )}

      {/* ─── Quick action result ─── */}
      {result && !isLoading && !showChat && (
        <div className="wig-result">
          <div
            className="wig-result-content md-content"
            dangerouslySetInnerHTML={{ __html: formatMarkdown(result) }}
          />
        </div>
      )}

      {/* ─── Inline chat ─── */}
      {showChat && (
        <div className="wig-chat">
          <div className="wig-chat-messages">
            {chatMessages.length === 0 && (
              <div className="wig-chat-hint">
                Ask anything about <strong>#{workItemId}</strong> — e.g. "What's
                missing in this story?" or "Update the priority to 1"
              </div>
            )}
            {chatMessages.map((msg, idx) => (
              <div key={idx} className={`wig-chat-msg wig-chat-${msg.role}`}>
                <span className="wig-chat-role">
                  {msg.role === "user" ? "You" : "Copilot"}
                </span>
                <div
                  className="wig-chat-content md-content"
                  dangerouslySetInnerHTML={{
                    __html: formatMarkdown(msg.content),
                  }}
                />
              </div>
            ))}
            {isChatLoading && (
              <div className="wig-chat-msg wig-chat-assistant">
                <span className="wig-chat-role">Copilot</span>
                <div className="wig-chat-typing">
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
            )}
            <div ref={chatEndRef} />
          </div>
          <div className="wig-chat-input-area">
            <textarea
              ref={chatInputRef}
              className="wig-chat-input"
              value={chatInput}
              onChange={(e) => setChatInput(e.target.value)}
              onKeyDown={handleChatKeyDown}
              placeholder={`Ask about #${workItemId}...`}
              rows={2}
              disabled={isChatLoading}
            />
            <button
              className="wig-chat-send"
              onClick={handleChatSend}
              disabled={!chatInput.trim() || isChatLoading}
              title="Send"
            >
              <svg
                width="14"
                height="14"
                viewBox="0 0 16 16"
                fill="currentColor"
              >
                <path d="M1 1.5L15 8L1 14.5V9.5L10 8L1 6.5V1.5Z" />
              </svg>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

ReactDOM.render(<WorkItemGroup />, document.getElementById("root"));
