/**
 * Shared TypeScript interfaces matching the backend API contracts.
 */

export interface ChatRequest {
  message: string;
  conversationHistory?: ConversationMessage[];
  projectName?: string;
  organizationUrl?: string;
  sessionId?: string;
  modelId?: string;
  workItemContext?: {
    id: number;
    title: string;
    type: string;
  };
}

export interface ConversationMessage {
  role: "user" | "assistant";
  content: string;
}

export interface ChatResponse {
  reply: string;
  workItems?: WorkItemSummary[];
  suggestedActions?: string[];
}

/**
 * Structured SSE event from the streaming chat endpoint.
 */
export interface StreamEvent {
  /** Event type: "step", "content", "content_replace", "suggestedActions", "error", "done" */
  type:
    | "step"
    | "content"
    | "content_replace"
    | "suggestedActions"
    | "error"
    | "done";
  /** Text content — token chunk, step description, or error message */
  content?: string;
  /** Step identifier for "step" events */
  step?:
    | "thinking"
    | "routing"
    | "agent"
    | "tool"
    | "formatting"
    | "correcting";
  /** Agent name for "agent" step events */
  agent?: string;
  /** Tool name for "tool" step events */
  tool?: string;
  /** Suggested actions for "suggestedActions" events */
  suggestedActions?: string[];
}

/**
 * A processing step shown in the UI while streaming.
 */
export interface ProcessingStep {
  id: string;
  label: string;
  status: "active" | "done";
  timestamp: Date;
}

/**
 * Model info from the backend /api/models endpoint.
 */
export interface ModelInfo {
  id: string;
  displayName: string;
  description?: string;
  isDefault: boolean;
}

export interface WorkItemSummary {
  id: number;
  title: string;
  workItemType: string;
  state: string;
  assignedTo?: string;
  areaPath?: string;
  iterationPath?: string;
  description?: string;
  url?: string;
  createdDate?: string;
  changedDate?: string;
  tags?: string;
  priority?: number;
  storyPoints?: number;
  valueArea?: string;
  customFields?: Record<string, unknown>;
}

export interface SessionInfo {
  sessionId: string;
  userId: string;
  projectName?: string;
  title: string;
  createdAt: string;
  lastActiveAt: string;
  messageCount: number;
  messages?: ConversationMessage[];
}

/**
 * Extension configuration stored in extension data service.
 */
export interface ExtensionSettings {
  backendUrl: string;
}

/**
 * State for the chat panel component.
 */
export interface ChatState {
  messages: ConversationMessage[];
  isLoading: boolean;
  error?: string;
  currentSessionId?: string;
}
