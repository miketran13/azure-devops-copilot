import * as React from "react";
import {
  Button,
  Text,
  Input,
  Spinner,
  makeStyles,
  tokens,
  shorthands,
  Divider,
  MessageBar,
  MessageBarBody,
} from "@fluentui/react-components";
import {
  AddRegular,
  DeleteRegular,
  DismissRegular,
  ChatRegular,
  SearchRegular,
  EditRegular,
  CheckmarkRegular,
} from "@fluentui/react-icons";
import {
  listSessions,
  deleteSession,
  getSession,
  updateSessionTitle,
} from "../services/sessionApi";
import type { ConversationMessage, SessionInfo } from "../models/types";
import { getDevOpsContext } from "../services/devopsContext";

export interface SessionPanelProps {
  onLoadSession: (messages: ConversationMessage[], sessionId: string) => void;
  onNewChat: () => void;
  onClose: () => void;
  currentSessionId?: string;
  /** Optional filter to scope sessions (e.g. by work item) */
  filterFn?: (session: SessionInfo) => boolean;
}

const useStyles = makeStyles({
  overlay: {
    position: "absolute",
    top: 0,
    left: 0,
    bottom: 0,
    width: "300px",
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderRight("1px", "solid", tokens.colorNeutralStroke2),
    display: "flex",
    flexDirection: "column",
    zIndex: 10,
    boxShadow: tokens.shadow16,
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    ...shorthands.padding("12px", "16px"),
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke2),
  },
  searchBox: {
    ...shorthands.padding("8px", "12px"),
  },
  list: {
    flexGrow: 1,
    overflowY: "auto",
    ...shorthands.padding("8px"),
  },
  groupLabel: {
    ...shorthands.padding("8px", "12px", "4px"),
    fontSize: "11px",
    fontWeight: 600 as unknown as string,
    textTransform: "uppercase" as const,
    letterSpacing: "0.5px",
    color: tokens.colorNeutralForeground3,
  },
  item: {
    display: "flex",
    alignItems: "center",
    ...shorthands.padding("10px", "12px"),
    ...shorthands.borderRadius("6px"),
    cursor: "pointer",
    ...shorthands.gap("8px"),
    transitionProperty: "background-color",
    transitionDuration: "0.1s",
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  itemActive: {
    backgroundColor: tokens.colorNeutralBackground1Selected,
  },
  itemText: {
    flexGrow: 1,
    minWidth: 0,
  },
  itemPreview: {
    whiteSpace: "nowrap",
    ...shorthands.overflow("hidden"),
    textOverflow: "ellipsis",
    display: "block",
    maxWidth: "100%",
  },
  itemActions: {
    display: "flex",
    ...shorthands.gap("2px"),
    flexShrink: 0,
  },
  renameInput: {
    width: "100%",
  },
  emptyState: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    flexGrow: 1,
    ...shorthands.gap("8px"),
    ...shorthands.padding("24px"),
    textAlign: "center" as const,
  },
});

type DateGroup = "Today" | "Yesterday" | "This Week" | "This Month" | "Older";

function getDateGroup(dateStr: string): DateGroup {
  const d = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffDays = Math.floor(diffMs / 86400000);
  if (diffDays === 0) return "Today";
  if (diffDays === 1) return "Yesterday";
  if (diffDays < 7) return "This Week";
  if (diffDays < 30) return "This Month";
  return "Older";
}

const DATE_GROUP_ORDER: DateGroup[] = [
  "Today",
  "Yesterday",
  "This Week",
  "This Month",
  "Older",
];

function formatDate(dateStr: string): string {
  const d = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffDays = Math.floor(diffMs / 86400000);
  if (diffDays === 0) return "Today";
  if (diffDays === 1) return "Yesterday";
  if (diffDays < 7) return `${diffDays}d ago`;
  return d.toLocaleDateString();
}

/**
 * Session history panel — lists past chat sessions grouped by date,
 * with search, inline rename, and delete.
 */
export function SessionPanel({
  onLoadSession,
  onNewChat,
  onClose,
  currentSessionId,
  filterFn,
}: SessionPanelProps): React.ReactElement {
  const styles = useStyles();
  const [sessions, setSessions] = React.useState<SessionInfo[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | undefined>();
  const [searchQuery, setSearchQuery] = React.useState("");
  const [renamingId, setRenamingId] = React.useState<string | undefined>();
  const [renameValue, setRenameValue] = React.useState("");

  const loadSessions = React.useCallback(async () => {
    setLoading(true);
    setError(undefined);
    try {
      const ctx = await getDevOpsContext();
      const list = await listSessions(ctx.projectName);
      setSessions(list);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load sessions");
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    loadSessions();
  }, [loadSessions]);

  const handleLoad = async (sessionId: string) => {
    if (renamingId) return; // Don't load while renaming
    try {
      const session = await getSession(sessionId);
      onLoadSession(session.messages ?? [], sessionId);
    } catch {
      setError("Failed to load session");
    }
  };

  const handleDelete = async (e: React.MouseEvent, sessionId: string) => {
    e.stopPropagation();
    try {
      await deleteSession(sessionId);
      setSessions((prev) => prev.filter((s) => s.sessionId !== sessionId));
    } catch {
      setError("Failed to delete session");
    }
  };

  const handleStartRename = (e: React.MouseEvent, session: SessionInfo) => {
    e.stopPropagation();
    setRenamingId(session.sessionId);
    setRenameValue(session.title || "");
  };

  const handleFinishRename = async (sessionId: string) => {
    const trimmed = renameValue.trim();
    if (trimmed) {
      // Optimistic update
      setSessions((prev) =>
        prev.map((s) =>
          s.sessionId === sessionId ? { ...s, title: trimmed } : s,
        ),
      );
      updateSessionTitle(sessionId, trimmed).catch(() => {
        // Revert on failure — reload
        loadSessions();
      });
    }
    setRenamingId(undefined);
    setRenameValue("");
  };

  const handleRenameKeyDown = (e: React.KeyboardEvent, sessionId: string) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleFinishRename(sessionId);
    } else if (e.key === "Escape") {
      setRenamingId(undefined);
      setRenameValue("");
    }
  };

  // Apply external filter + search query
  const filteredSessions = React.useMemo(() => {
    let result = sessions;
    if (filterFn) {
      result = result.filter(filterFn);
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter((s) => (s.title || "").toLowerCase().includes(q));
    }
    return result;
  }, [sessions, filterFn, searchQuery]);

  // Group sessions by date
  const groupedSessions = React.useMemo(() => {
    const groups = new Map<DateGroup, SessionInfo[]>();
    for (const session of filteredSessions) {
      const group = getDateGroup(session.lastActiveAt);
      if (!groups.has(group)) groups.set(group, []);
      groups.get(group)!.push(session);
    }
    return groups;
  }, [filteredSessions]);

  return (
    <div className={styles.overlay}>
      <div className={styles.header}>
        <Text size={400} weight="semibold">
          Chat History
        </Text>
        <div style={{ display: "flex", gap: "4px" }}>
          <Button
            appearance="subtle"
            icon={<AddRegular />}
            size="small"
            onClick={onNewChat}
            title="New chat"
          />
          <Button
            appearance="subtle"
            icon={<DismissRegular />}
            size="small"
            onClick={onClose}
            title="Close"
          />
        </div>
      </div>

      {/* Search */}
      <div className={styles.searchBox}>
        <Input
          contentBefore={<SearchRegular fontSize={14} />}
          placeholder="Search sessions..."
          size="small"
          value={searchQuery}
          onChange={(_e, data) => setSearchQuery(data.value)}
          style={{ width: "100%" }}
        />
      </div>

      {error && (
        <MessageBar intent="error" style={{ margin: 8 }}>
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {loading ? (
        <div className={styles.emptyState}>
          <Spinner size="small" label="Loading sessions..." />
        </div>
      ) : filteredSessions.length === 0 ? (
        <div className={styles.emptyState}>
          <ChatRegular fontSize={32} />
          <Text size={200}>
            {searchQuery ? "No matching sessions" : "No previous sessions"}
          </Text>
          <Text size={100}>
            {searchQuery
              ? "Try a different search term."
              : "Start a new conversation to create one."}
          </Text>
        </div>
      ) : (
        <div className={styles.list}>
          {DATE_GROUP_ORDER.map((group) => {
            const groupSessions = groupedSessions.get(group);
            if (!groupSessions || groupSessions.length === 0) return null;
            return (
              <React.Fragment key={group}>
                <div className={styles.groupLabel}>{group}</div>
                {groupSessions.map((session) => (
                  <div
                    key={session.sessionId}
                    className={`${styles.item} ${session.sessionId === currentSessionId ? styles.itemActive : ""}`}
                    onClick={() => handleLoad(session.sessionId)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) =>
                      e.key === "Enter" && handleLoad(session.sessionId)
                    }
                  >
                    <div className={styles.itemText}>
                      {renamingId === session.sessionId ? (
                        <Input
                          className={styles.renameInput}
                          size="small"
                          value={renameValue}
                          onChange={(_e, data) => setRenameValue(data.value)}
                          onKeyDown={(e) =>
                            handleRenameKeyDown(e, session.sessionId)
                          }
                          onBlur={() => handleFinishRename(session.sessionId)}
                          autoFocus
                          onClick={(e) => e.stopPropagation()}
                        />
                      ) : (
                        <>
                          <Text size={200} weight="semibold" truncate block>
                            {session.title || "Untitled"}
                          </Text>
                          <Text
                            size={100}
                            className={styles.itemPreview}
                            style={{
                              color: tokens.colorNeutralForeground3,
                            }}
                          >
                            {formatDate(session.lastActiveAt)} ·{" "}
                            {session.messageCount} msgs
                          </Text>
                        </>
                      )}
                    </div>
                    {renamingId !== session.sessionId && (
                      <div className={styles.itemActions}>
                        <Button
                          appearance="subtle"
                          icon={<EditRegular />}
                          size="small"
                          onClick={(e) => handleStartRename(e, session)}
                          title="Rename session"
                        />
                        <Button
                          appearance="subtle"
                          icon={<DeleteRegular />}
                          size="small"
                          onClick={(e) => handleDelete(e, session.sessionId)}
                          title="Delete session"
                        />
                      </div>
                    )}
                  </div>
                ))}
              </React.Fragment>
            );
          })}
        </div>
      )}
    </div>
  );
}
