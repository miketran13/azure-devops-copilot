import * as React from "react";
import { chat } from "../services/backendApi";
import type { ConversationMessage, WorkItemSummary } from "../models/types";
import { MessageBubble } from "./MessageBubble";
import { WorkItemCard } from "./WorkItemCard";

import "./ChatPanel.scss";

interface ChatMessage {
  role: "user" | "assistant";
  content: string;
  workItems?: WorkItemSummary[];
  suggestedActions?: string[];
  timestamp: Date;
}

/**
 * Main chat panel component — message list, input box, and suggested actions.
 */
export function ChatPanel(): React.ReactElement {
  const [messages, setMessages] = React.useState<ChatMessage[]>([]);
  const [input, setInput] = React.useState("");
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();
  const messagesEndRef = React.useRef<HTMLDivElement>(null);
  const inputRef = React.useRef<HTMLTextAreaElement>(null);

  // Auto-scroll to bottom when messages change
  React.useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // Focus input on mount
  React.useEffect(() => {
    inputRef.current?.focus();
  }, []);

  const handleSend = React.useCallback(
    async (messageText?: string) => {
      const text = messageText ?? input.trim();
      if (!text || isLoading) return;

      setInput("");
      setError(undefined);

      // Add user message
      const userMessage: ChatMessage = {
        role: "user",
        content: text,
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, userMessage]);
      setIsLoading(true);

      try {
        // Build conversation history for context
        const history: ConversationMessage[] = messages.map((m) => ({
          role: m.role,
          content: m.content,
        }));

        const response = await chat(text, history);

        const assistantMessage: ChatMessage = {
          role: "assistant",
          content: response.reply,
          workItems: response.workItems,
          suggestedActions: response.suggestedActions,
          timestamp: new Date(),
        };
        setMessages((prev) => [...prev, assistantMessage]);
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
      } finally {
        setIsLoading(false);
        inputRef.current?.focus();
      }
    },
    [input, isLoading, messages],
  );

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleSuggestedAction = (action: string) => {
    handleSend(action);
  };

  // Get suggested actions from the last assistant message
  const lastAssistantMessage = [...messages]
    .reverse()
    .find((m) => m.role === "assistant");
  const suggestedActions = lastAssistantMessage?.suggestedActions;

  return (
    <div className="chat-panel">
      {/* Messages area */}
      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-welcome">
            <h2>Welcome to DevOps Copilot</h2>
            <p>I can help you with Azure DevOps work items. Try asking:</p>
            <div className="welcome-suggestions">
              <button
                onClick={() =>
                  handleSend("Show me all active bugs assigned to me")
                }
              >
                Show me all active bugs assigned to me
              </button>
              <button
                onClick={() =>
                  handleSend("Create a new user story for the login feature")
                }
              >
                Create a new user story for the login feature
              </button>
              <button
                onClick={() =>
                  handleSend("Analyze the requirements in the current sprint")
                }
              >
                Analyze the requirements in the current sprint
              </button>
              <button
                onClick={() =>
                  handleSend("What work items were updated today?")
                }
              >
                What work items were updated today?
              </button>
            </div>
          </div>
        )}

        {messages.map((msg, idx) => (
          <div key={idx} className={`message-container message-${msg.role}`}>
            <MessageBubble
              role={msg.role}
              content={msg.content}
              timestamp={msg.timestamp}
            />

            {/* Work item cards */}
            {msg.workItems && msg.workItems.length > 0 && (
              <div className="work-items-grid">
                {msg.workItems.map((wi) => (
                  <WorkItemCard key={wi.id} workItem={wi} />
                ))}
              </div>
            )}
          </div>
        ))}

        {isLoading && (
          <div className="message-container message-assistant">
            <div className="typing-indicator">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Suggested actions */}
      {suggestedActions && suggestedActions.length > 0 && !isLoading && (
        <div className="suggested-actions">
          {suggestedActions.map((action, idx) => (
            <button
              key={idx}
              className="suggestion-chip"
              onClick={() => handleSuggestedAction(action)}
            >
              {action}
            </button>
          ))}
        </div>
      )}

      {/* Error banner */}
      {error && (
        <div className="chat-error">
          <span>{error}</span>
          <button onClick={() => setError(undefined)}>✕</button>
        </div>
      )}

      {/* Input area */}
      <div className="chat-input-area">
        <textarea
          ref={inputRef}
          className="chat-input"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about your work items... (Enter to send, Shift+Enter for new line)"
          rows={2}
          disabled={isLoading}
        />
        <button
          className="send-button"
          onClick={() => handleSend()}
          disabled={!input.trim() || isLoading}
          title="Send message"
        >
          <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
            <path d="M1 1.5L15 8L1 14.5V9.5L10 8L1 6.5V1.5Z" />
          </svg>
        </button>
      </div>
    </div>
  );
}
