import type { SessionInfo } from "../models/types";
import { getDevOpsContext } from "./devopsContext";
import { getBackendUrl, getAuthHeaders } from "./backendApi";

/**
 * Session API client — CRUD operations for persistent chat sessions.
 * Communicates with the backend /api/sessions endpoints.
 */

async function headers(): Promise<Record<string, string>> {
  const authHeaders = await getAuthHeaders();
  const { userId } = await getDevOpsContext();
  return {
    "Content-Type": "application/json",
    ...authHeaders,
    ...(userId ? { "X-User-Id": userId } : {}),
  };
}

export async function listSessions(
  projectName?: string,
  skip = 0,
  take = 50,
): Promise<SessionInfo[]> {
  const params = new URLSearchParams();
  if (projectName) params.set("projectName", projectName);
  if (skip > 0) params.set("skip", skip.toString());
  params.set("take", take.toString());

  const response = await fetch(
    `${getBackendUrl()}/sessions?${params.toString()}`,
    { headers: await headers() },
  );
  if (!response.ok)
    throw new Error(`Failed to list sessions (${response.status})`);
  return (await response.json()) as SessionInfo[];
}

export async function getSession(sessionId: string): Promise<SessionInfo> {
  const response = await fetch(`${getBackendUrl()}/sessions/${sessionId}`, {
    headers: await headers(),
  });
  if (!response.ok)
    throw new Error(`Failed to get session (${response.status})`);
  return (await response.json()) as SessionInfo;
}

export async function createSession(
  projectName?: string,
  title?: string,
): Promise<SessionInfo> {
  const response = await fetch(`${getBackendUrl()}/sessions`, {
    method: "POST",
    headers: await headers(),
    body: JSON.stringify({ projectName, title }),
  });
  if (!response.ok)
    throw new Error(`Failed to create session (${response.status})`);
  return (await response.json()) as SessionInfo;
}

export async function deleteSession(sessionId: string): Promise<void> {
  const response = await fetch(`${getBackendUrl()}/sessions/${sessionId}`, {
    method: "DELETE",
    headers: await headers(),
  });
  if (!response.ok)
    throw new Error(`Failed to delete session (${response.status})`);
}

export async function addMessage(
  sessionId: string,
  role: "user" | "assistant",
  content: string,
): Promise<void> {
  const response = await fetch(
    `${getBackendUrl()}/sessions/${sessionId}/messages`,
    {
      method: "POST",
      headers: await headers(),
      body: JSON.stringify({ role, content }),
    },
  );
  if (!response.ok)
    throw new Error(`Failed to add message (${response.status})`);
}

export async function updateSessionTitle(
  sessionId: string,
  title: string,
): Promise<void> {
  const response = await fetch(`${getBackendUrl()}/sessions/${sessionId}`, {
    method: "PATCH",
    headers: await headers(),
    body: JSON.stringify({ title }),
  });
  if (!response.ok)
    throw new Error(`Failed to update session (${response.status})`);
}
