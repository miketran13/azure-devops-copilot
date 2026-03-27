import * as React from "react";
import {
  Button,
  Text,
  Input,
  Spinner,
  makeStyles,
  tokens,
  shorthands,
  MessageBar,
  MessageBarBody,
} from "@fluentui/react-components";
import {
  DeleteRegular,
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

export interface SessionListProps {
  onLoadSession: (messages: ConversationMessage[], sessionId: string) => void;
  currentSessionId?: string;
  filterFn?: (session: SessionInfo) => boolean;
  compact?: boolean;
  /** Increment to trigger a session list refresh (e.g. after a new session is created) */
  refreshTrigger?: number;
}

type DateGroup = "Today" | "Yesterday" | "This Week" | "This Month" | "Older";

const DATE_GROUP_ORDER: DateGroup[] = [
  "Today",
  "Yesterday",
  "This Week",
  "This Month",
  "Older",
];

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

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    flexGrow: 1,
    minHeight: 0,
  },
  searchBox: {
    ...shorthands.padding("8px", "12px"),
    flexShrink: 0,
  },
  list: {
    flexGrow: 1,
    overflowY: "auto",
    ...shorthands.padding("4px", "8px"),
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
    ...shorthands.padding("8px", "10px"),
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
    opacity: 0,
    transitionProperty: "opacity",
    transitionDuration: "0.1s",
    "$item:hover &": {
      opacity: 1,
    },
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

export function SessionList({
  onLoadSession,
  currentSessionId,
  filterFn,
  compact,
  refreshTrigger,
}: SessionListProps): React.ReactElement {
  const styles = useStyles();
  const [sessions, setSessions] = React.useState<SessionInfo[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | undefined>();
  const [searchQuery, setSearchQuery] = React.useState("");
  const [renamingId, setRenamingId] = React.useState<string | undefined>();
  const [renameValue, setRenameValue] = React.useState("");
  const [hoveredId, setHoveredId] = React.useState<string | undefined>();
  const [loadingId, setLoadingId] = React.useState<string | undefined>();

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

  // Refresh when triggered externally (e.g. after a new session is created mid-chat)
  React.useEffect(() => {
    if (refreshTrigger !== undefined && refreshTrigger > 0) {
      loadSessions();
    }
  }, [refreshTrigger]);

  const handleLoad = async (sessionId: string) => {
    if (renamingId || loadingId) return;
    setLoadingId(sessionId);
    try {
      const session = await getSession(sessionId);
      onLoadSession(session.messages ?? [], sessionId);
    } catch {
      setError("Failed to load session");
    } finally {
      setLoadingId(undefined);
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
      setSessions((prev) =>
        prev.map((s) =>
          s.sessionId === sessionId ? { ...s, title: trimmed } : s,
        ),
      );
      updateSessionTitle(sessionId, trimmed).catch(() => {
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
    <div className={styles.container}>
      {/* Search */}
      <div className={styles.searchBox}>
        <Input
          contentBefore={<SearchRegular fontSize={14} />}
          placeholder="Search chats..."
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
          <Spinner size="small" label="Loading..." />
        </div>
      ) : filteredSessions.length === 0 ? (
        <div className={styles.emptyState}>
          <ChatRegular fontSize={28} />
          <Text size={200}>
            {searchQuery ? "No matching chats" : "No chats yet"}
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
                    onMouseEnter={() => setHoveredId(session.sessionId)}
                    onMouseLeave={() => setHoveredId(undefined)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) =>
                      e.key === "Enter" && handleLoad(session.sessionId)
                    }
                  >
                    {loadingId === session.sessionId ? (
                      <Spinner size="extra-tiny" style={{ flexShrink: 0 }} />
                    ) : (
                      <ChatRegular
                        fontSize={16}
                        style={{
                          flexShrink: 0,
                          color: tokens.colorNeutralForeground3,
                        }}
                      />
                    )}
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
                          {!compact && (
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
                          )}
                        </>
                      )}
                    </div>
                    {renamingId !== session.sessionId &&
                      hoveredId === session.sessionId && (
                        <div className={styles.itemActions}>
                          <Button
                            appearance="subtle"
                            icon={<EditRegular />}
                            size="small"
                            onClick={(e) => handleStartRename(e, session)}
                            title="Rename"
                          />
                          <Button
                            appearance="subtle"
                            icon={<DeleteRegular />}
                            size="small"
                            onClick={(e) => handleDelete(e, session.sessionId)}
                            title="Delete"
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
