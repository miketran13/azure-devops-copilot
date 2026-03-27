import { getAccessToken, getDevOpsContext } from "./devopsContext";

/**
 * Fetch all project names from Azure DevOps directly using the user's token.
 */
export async function listProjects(): Promise<string[]> {
  const { organizationUrl } = await getDevOpsContext();
  const accessToken = await getAccessToken();

  const response = await fetch(
    `${organizationUrl}/_apis/projects?api-version=7.1&$top=200`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        Accept: "application/json",
      },
    },
  );

  if (!response.ok) {
    throw new Error(`Failed to list projects (${response.status})`);
  }

  const json = await response.json();
  const projects: string[] = (json.value ?? [])
    .map((p: { name?: string }) => p.name)
    .filter((n: string | undefined): n is string => !!n)
    .sort();

  return projects;
}
