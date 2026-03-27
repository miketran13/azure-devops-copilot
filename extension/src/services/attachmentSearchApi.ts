import { getAccessToken, getDevOpsContext } from "./devopsContext";
import type { AttachmentItem } from "../components/AttachmentChip";

/**
 * Search work items by numeric ID or title using WIQL.
 */
export async function searchWorkItems(
  query: string,
  projectName: string,
): Promise<AttachmentItem[]> {
  const { organizationUrl } = await getDevOpsContext();
  const accessToken = await getAccessToken();

  const numericId = parseInt(query, 10);
  let wiql: string;
  if (query.trim() && !isNaN(numericId)) {
    wiql = `SELECT [System.Id],[System.Title],[System.WorkItemType] FROM WorkItems WHERE [System.TeamProject] = '${projectName}' AND [System.Id] = ${numericId}`;
  } else if (query.trim()) {
    const escaped = query.replace(/'/g, "''");
    wiql = `SELECT [System.Id],[System.Title],[System.WorkItemType] FROM WorkItems WHERE [System.TeamProject] = '${projectName}' AND [System.Title] CONTAINS '${escaped}' ORDER BY [System.ChangedDate] DESC`;
  } else {
    wiql = `SELECT [System.Id],[System.Title],[System.WorkItemType] FROM WorkItems WHERE [System.TeamProject] = '${projectName}' ORDER BY [System.ChangedDate] DESC`;
  }

  const wiqlResponse = await fetch(
    `${organizationUrl}/${encodeURIComponent(projectName)}/_apis/wit/wiql?api-version=7.1&$top=20`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({ query: wiql }),
    },
  );

  if (!wiqlResponse.ok) return [];

  const wiqlJson = await wiqlResponse.json();
  const ids: number[] = (wiqlJson.workItems ?? [])
    .slice(0, 20)
    .map((wi: { id: number }) => wi.id);

  if (ids.length === 0) return [];

  const detailsResponse = await fetch(
    `${organizationUrl}/_apis/wit/workitems?ids=${ids.join(",")}&fields=System.Id,System.Title,System.WorkItemType&api-version=7.1`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        Accept: "application/json",
      },
    },
  );

  if (!detailsResponse.ok) return [];
  const detailsJson = await detailsResponse.json();

  return (detailsJson.value ?? []).map(
    (wi: { id: number; fields: Record<string, string> }) => ({
      type: "workItem" as const,
      id: String(wi.id),
      label: wi.fields["System.Title"] ?? `Work Item #${wi.id}`,
      secondaryLabel: `#${wi.id} · ${wi.fields["System.WorkItemType"] ?? ""}`,
    }),
  );
}

/**
 * Search repositories in the project by name.
 */
export async function searchRepositories(
  query: string,
  projectName: string,
): Promise<AttachmentItem[]> {
  const { organizationUrl } = await getDevOpsContext();
  const accessToken = await getAccessToken();

  const response = await fetch(
    `${organizationUrl}/${encodeURIComponent(projectName)}/_apis/git/repositories?api-version=7.1`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        Accept: "application/json",
      },
    },
  );

  if (!response.ok) return [];

  const json = await response.json();
  const repos: { id: string; name: string }[] = json.value ?? [];

  const filtered = query.trim()
    ? repos.filter((r) => r.name.toLowerCase().includes(query.toLowerCase()))
    : repos;

  return filtered.slice(0, 20).map((r) => ({
    type: "repository" as const,
    id: r.id,
    label: r.name,
    secondaryLabel: "Repository",
  }));
}

/**
 * Search team members across the project's teams by display name or email.
 */
export async function searchMembers(
  query: string,
  projectName: string,
): Promise<AttachmentItem[]> {
  const { organizationUrl } = await getDevOpsContext();
  const accessToken = await getAccessToken();

  const teamsResponse = await fetch(
    `${organizationUrl}/_apis/projects/${encodeURIComponent(projectName)}/teams?api-version=7.1&$top=10`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        Accept: "application/json",
      },
    },
  );

  if (!teamsResponse.ok) return [];

  const teamsJson = await teamsResponse.json();
  const teams: { id: string; name: string }[] = teamsJson.value ?? [];
  if (teams.length === 0) return [];

  const memberMap = new Map<string, AttachmentItem>();

  for (const team of teams.slice(0, 3)) {
    const membersResponse = await fetch(
      `${organizationUrl}/_apis/projects/${encodeURIComponent(projectName)}/teams/${team.id}/members?api-version=7.1`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/json",
        },
      },
    );

    if (!membersResponse.ok) continue;

    const membersJson = await membersResponse.json();
    const members: {
      identity: { id: string; displayName: string; uniqueName: string };
    }[] = membersJson.value ?? [];

    for (const m of members) {
      if (!memberMap.has(m.identity.id)) {
        memberMap.set(m.identity.id, {
          type: "member" as const,
          id: m.identity.id,
          label: m.identity.displayName,
          secondaryLabel: m.identity.uniqueName,
        });
      }
    }
  }

  const all = Array.from(memberMap.values());
  const filtered = query.trim()
    ? all.filter(
        (m) =>
          m.label.toLowerCase().includes(query.toLowerCase()) ||
          (m.secondaryLabel ?? "").toLowerCase().includes(query.toLowerCase()),
      )
    : all;

  return filtered.slice(0, 20);
}
