import * as SDK from "azure-devops-extension-sdk";

/**
 * Extracts the current Azure DevOps context from the extension SDK.
 */

export interface DevOpsContext {
  projectName: string;
  organizationUrl: string;
  userName: string;
  userId: string;
}

/**
 * Get the current Azure DevOps project and organization context.
 */
export async function getDevOpsContext(): Promise<DevOpsContext> {
  const host = SDK.getHost();
  const user = SDK.getUser();

  // The host name is the organization name
  const organizationUrl = `https://dev.azure.com/${host.name}`;

  // Get project context from the page context
  const projectName = await getProjectName();

  return {
    projectName,
    organizationUrl,
    userName: user.displayName,
    userId: user.id,
  };
}

/**
 * Get the current project name from the contribution context.
 */
async function getProjectName(): Promise<string> {
  // getWebContext is the most reliable SDK method for project info in Hub contributions
  try {
    const webContext = SDK.getWebContext();
    if (webContext?.project?.name) {
      return webContext.project.name;
    }
  } catch {
    // Not in a project context, continue to fallbacks
  }

  // Fall back to SDK configuration object
  try {
    const context = SDK.getConfiguration();
    if (context && typeof context === "object" && "projectName" in context) {
      return context.projectName as string;
    }
  } catch {
    // Continue to URL fallback
  }

  // Extract project name from the current page URL
  const url = window.location.href;
  const match = url.match(/dev\.azure\.com\/[^/]+\/([^/]+)/);
  if (match) {
    return decodeURIComponent(match[1]);
  }

  return "";
}

/**
 * Get a user access token for calling Azure DevOps APIs or the backend.
 */
export async function getAccessToken(): Promise<string> {
  return await SDK.getAccessToken();
}

/**
 * Get the app token (JWT signed with extension certificate) for backend authentication.
 */
export async function getAppToken(): Promise<string> {
  return await SDK.getAppToken();
}
