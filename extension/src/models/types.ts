/**
 * Shared TypeScript interfaces matching the backend API contracts.
 */

export interface ChatRequest {
  message: string;
  conversationHistory?: ConversationMessage[];
  projectName?: string;
  organizationUrl?: string;
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
}
