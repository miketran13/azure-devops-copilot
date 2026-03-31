/**
 * Standalone context service — replaces devopsContext.ts for standalone mode.
 * Reads user configuration from localStorage instead of the Azure DevOps SDK.
 */

const STORAGE_KEYS = {
  githubToken: "devops-copilot-github-token",
  backendUrl: "devops-copilot-backend-url",
  adoOrgUrl: "devops-copilot-ado-org-url",
  adoPat: "devops-copilot-ado-pat",
  projectName: "devops-copilot-project-name",
} as const;

export interface StandaloneSettings {
  githubToken: string;
  backendUrl: string;
  adoOrgUrl: string;
  adoPat: string;
  projectName: string;
}

export function getStandaloneSettings(): StandaloneSettings {
  return {
    githubToken: localStorage.getItem(STORAGE_KEYS.githubToken) ?? "",
    backendUrl:
      localStorage.getItem(STORAGE_KEYS.backendUrl) ||
      (process.env.BACKEND_URL ?? "http://localhost:7071/api"),
    adoOrgUrl: localStorage.getItem(STORAGE_KEYS.adoOrgUrl) ?? "",
    adoPat: localStorage.getItem(STORAGE_KEYS.adoPat) ?? "",
    projectName: localStorage.getItem(STORAGE_KEYS.projectName) ?? "",
  };
}

export function saveStandaloneSettings(settings: StandaloneSettings): void {
  localStorage.setItem(STORAGE_KEYS.githubToken, settings.githubToken);
  localStorage.setItem(STORAGE_KEYS.backendUrl, settings.backendUrl);
  localStorage.setItem(STORAGE_KEYS.adoOrgUrl, settings.adoOrgUrl);
  localStorage.setItem(STORAGE_KEYS.adoPat, settings.adoPat);
  localStorage.setItem(STORAGE_KEYS.projectName, settings.projectName);
}

/**
 * Check if the standalone mode has minimum required configuration (GitHub PAT).
 */
export function isConfigured(): boolean {
  const settings = getStandaloneSettings();
  return settings.githubToken.length > 0;
}

/**
 * Generate a stable user ID from the GitHub token (first 16 chars of a simple hash).
 * This matches the backend's rate-limit key generation approach.
 */
export function getUserId(): string {
  const token = getStandaloneSettings().githubToken;
  if (!token) return "anonymous";
  // Simple hash for user identification (not security-critical)
  let hash = 0;
  for (let i = 0; i < token.length; i++) {
    const char = token.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash |= 0;
  }
  return `standalone-${Math.abs(hash).toString(16)}`;
}
