import type {
  ChatRequest,
  ChatResponse,
  ExtensionSettings,
  StreamEvent,
  ModelInfo,
} from "../models/types";
import { getAccessToken, getAppToken, getDevOpsContext } from "./devopsContext";

/**
 * Backend URL — injected at build time via webpack DefinePlugin.
 * Local dev: http://localhost:7071/api (default)
 * Production: set BACKEND_URL env var during build (e.g. https://devopscopilot-func.azurewebsites.net/api)
 */
const DEFAULT_BACKEND_URL: string = process.env.BACKEND_URL!;

let _backendUrl: string = DEFAULT_BACKEND_URL;

/**
 * Configure the backend API URL.
 */
export function setBackendUrl(url: string): void {
  _backendUrl = url.replace(/\/$/, ""); // Remove trailing slash
}

/**
 * Get current backend URL.
 */
export function getBackendUrl(): string {
  return _backendUrl;
}

/**
 * Send a chat message to the backend and get a response.
 */
export async function chat(
  message: string,
  conversationHistory?: { role: "user" | "assistant"; content: string }[],
  modelId?: string,
): Promise<ChatResponse> {
  const context = await getDevOpsContext();
  const accessToken = await getAccessToken();
  const appToken = await getAppToken();

  const request: ChatRequest = {
    message,
    conversationHistory,
    projectName: context.projectName,
    organizationUrl: context.organizationUrl,
    modelId,
  };

  const response = await fetch(`${_backendUrl}/chat`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
      "X-Extension-Token": appToken,
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorBody = await response.text();
    throw new Error(
      `Backend request failed (${response.status}): ${errorBody}`,
    );
  }

  return (await response.json()) as ChatResponse;
}

/**
 * Send a chat message with streaming response (Server-Sent Events).
 * Calls onEvent for each structured StreamEvent from the backend.
 * Events include processing steps, content tokens, suggested actions, and errors.
 */
export async function chatStream(
  message: string,
  onEvent: (event: StreamEvent) => void,
  conversationHistory?: { role: "user" | "assistant"; content: string }[],
  modelId?: string,
  overrides?: {
    projectName?: string;
    workItemContext?: { id: number; title: string; type: string };
  },
  signal?: AbortSignal,
): Promise<void> {
  const context = await getDevOpsContext();
  const accessToken = await getAccessToken();
  const appToken = await getAppToken();

  const request: ChatRequest = {
    message,
    conversationHistory,
    projectName: overrides?.projectName || context.projectName,
    organizationUrl: context.organizationUrl,
    modelId,
    workItemContext: overrides?.workItemContext,
  };

  const response = await fetch(`${_backendUrl}/chat/stream`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
      "X-Extension-Token": appToken,
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const errorBody = await response.text();
    throw new Error(
      `Backend stream request failed (${response.status}): ${errorBody}`,
    );
  }

  const reader = response.body?.getReader();
  if (!reader) throw new Error("No response body");

  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    // Process SSE lines
    const lines = buffer.split("\n");
    buffer = lines.pop() ?? ""; // Keep incomplete line in buffer

    for (const line of lines) {
      if (line.startsWith("data: ")) {
        const data = line.slice(6).trim();
        if (data === "[DONE]") return;

        try {
          const parsed = JSON.parse(data) as StreamEvent;
          onEvent(parsed);

          if (parsed.type === "done") return;
          if (parsed.type === "error") {
            throw new Error(parsed.content ?? "Unknown streaming error");
          }
        } catch (e) {
          if (e instanceof SyntaxError) continue; // Skip malformed JSON
          throw e;
        }
      }
    }
  }
}

/**
 * Fetch available AI models from the backend.
 */
export async function fetchModels(): Promise<ModelInfo[]> {
  const response = await fetch(`${_backendUrl}/models`);
  if (!response.ok) {
    console.warn("Failed to fetch models, using empty list");
    return [];
  }
  return (await response.json()) as ModelInfo[];
}

/**
 * Quick convenience — analyze a specific work item.
 */
export async function analyzeWorkItem(
  workItemId: number,
): Promise<ChatResponse> {
  return chat(
    `Analyze work item #${workItemId} for requirement quality, completeness, and suggest improvements.`,
  );
}

/**
 * Quick convenience — generate test cases for a work item.
 */
export async function generateTestCases(
  workItemId: number,
): Promise<ChatResponse> {
  return chat(
    `Generate test cases for work item #${workItemId}. Include happy path, edge cases, and error scenarios.`,
  );
}

/**
 * Quick convenience — suggest child items for a Feature/Epic.
 */
export async function suggestChildItems(
  workItemId: number,
): Promise<ChatResponse> {
  return chat(
    `Suggest child user stories or tasks for work item #${workItemId}. Analyze the current description and existing children.`,
  );
}

/**
 * Quick convenience — generate acceptance criteria for a work item.
 */
export async function generateAcceptanceCriteria(
  workItemId: number,
): Promise<ChatResponse> {
  return chat(
    `Generate detailed acceptance criteria for work item #${workItemId}. ` +
      `Use the Given/When/Then format where appropriate. ` +
      `Cover functional requirements, edge cases, and non-functional aspects.`,
  );
}
