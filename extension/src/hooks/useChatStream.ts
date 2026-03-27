import * as React from "react";
import { chatStream } from "../services/backendApi";
import { createSession, addMessage } from "../services/sessionApi";
import { getDevOpsContext } from "../services/devopsContext";
import type {
  ConversationMessage,
  StreamEvent,
  ProcessingStep,
} from "../models/types";
import { STRINGS } from "../utils/strings";

export interface ChatMessage {
  role: "user" | "assistant";
  content: string;
  workItems?: unknown[];
  suggestedActions?: string[];
  timestamp: Date;
}

interface UseChatStreamOptions {
  /** Work item context to include in requests */
  workItemContext?: {
    id: number;
    title: string;
    type: string;
  };
  /** Selected AI model ID */
  selectedModelId?: string;
  /** Selected project name */
  selectedProject?: string;
  /** Injected work item IDs to tag */
  injectedWiIds?: number[];
}

interface UseChatStreamResult {
  messages: ChatMessage[];
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
  isLoading: boolean;
  error: string | undefined;
  setError: React.Dispatch<React.SetStateAction<string | undefined>>;
  streamingContent: string;
  processingSteps: ProcessingStep[];
  currentSessionId: string | undefined;
  setCurrentSessionId: React.Dispatch<React.SetStateAction<string | undefined>>;
  sendMessage: (text: string) => Promise<void>;
  resetChat: () => void;
}

/**
 * Shared hook for streaming chat functionality.
 * Extracts the common streaming logic used by ChatPanel and WorkItemGroup
 * into a reusable hook.
 */
export function useChatStream(
  options: UseChatStreamOptions,
): UseChatStreamResult {
  const [messages, setMessages] = React.useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();
  const [streamingContent, setStreamingContent] = React.useState("");
  const [processingSteps, setProcessingSteps] = React.useState<
    ProcessingStep[]
  >([]);
  const [currentSessionId, setCurrentSessionId] = React.useState<
    string | undefined
  >();

  const sendMessage = React.useCallback(
    async (text: string) => {
      if (!text || isLoading) return;

      setError(undefined);
      setStreamingContent("");
      setProcessingSteps([]);

      // Build display text with injected WI IDs
      let displayText = text;
      if (options.injectedWiIds && options.injectedWiIds.length > 0) {
        const idList = options.injectedWiIds.map((id) => `#${id}`).join(", ");
        displayText = `[Work items: ${idList}]\n${text}`;
      }

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
          const ctx = await getDevOpsContext();
          const sessionTitle = options.workItemContext
            ? `#${options.workItemContext.id} - ${options.workItemContext.title}`.slice(
                0,
                60,
              )
            : text.slice(0, 60);
          const session = await createSession(ctx.projectName, sessionTitle);
          sessionId = session.sessionId;
          setCurrentSessionId(sessionId);
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
          displayText,
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
              case "content_replace":
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
                setProcessingSteps((prev) =>
                  prev.map((s) => ({ ...s, status: "done" as const })),
                );
                break;
            }
          },
          history,
          options.selectedModelId || undefined,
          {
            projectName: options.selectedProject || undefined,
            workItemContext: options.workItemContext,
          },
        );

        const assistantMessage: ChatMessage = {
          role: "assistant",
          content: fullContent || STRINGS.chat.fallbackResponse,
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
          err instanceof Error ? err.message : STRINGS.errors.unknownError;
        setError(errorMessage);
        const errorAssistant: ChatMessage = {
          role: "assistant",
          content: `${STRINGS.chat.errorPrefix} ${errorMessage}`,
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
      isLoading,
      messages,
      currentSessionId,
      options.selectedModelId,
      options.selectedProject,
      options.injectedWiIds,
      options.workItemContext,
    ],
  );

  const resetChat = React.useCallback(() => {
    setMessages([]);
    setCurrentSessionId(undefined);
    setError(undefined);
    setStreamingContent("");
    setProcessingSteps([]);
  }, []);

  return {
    messages,
    setMessages,
    isLoading,
    error,
    setError,
    streamingContent,
    processingSteps,
    currentSessionId,
    setCurrentSessionId,
    sendMessage,
    resetChat,
  };
}
