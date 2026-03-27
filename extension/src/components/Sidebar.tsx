import * as React from "react";
import {
  Button,
  Tooltip,
  Divider,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  AddRegular,
  ChatRegular,
  PanelLeftRegular,
  PanelLeftExpandRegular,
  PersonRegular,
} from "@fluentui/react-icons";
import { SessionList } from "./SessionList";
import type { ConversationMessage } from "../models/types";

export interface SidebarProps {
  isCollapsed: boolean;
  onToggleCollapse: () => void;
  onNewChat: () => void;
  onLoadSession: (messages: ConversationMessage[], sessionId: string) => void;
  currentSessionId?: string;
  /** Increment to force the session list to refresh */
  refreshTrigger?: number;
}

const SIDEBAR_WIDTH = 260;
const SIDEBAR_COLLAPSED_WIDTH = 48;

const useStyles = makeStyles({
  sidebar: {
    display: "flex",
    flexDirection: "column",
    height: "100%",
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderRight("1px", "solid", tokens.colorNeutralStroke2),
    transitionProperty: "width",
    transitionDuration: "0.2s",
    transitionTimingFunction: "ease",
    overflowX: "hidden",
    flexShrink: 0,
  },
  topSection: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.padding("8px"),
    ...shorthands.gap("4px"),
    flexShrink: 0,
  },
  newChatButton: {
    justifyContent: "flex-start",
    width: "100%",
    fontWeight: 600 as unknown as string,
  },
  newChatButtonCollapsed: {
    justifyContent: "center",
    minWidth: "32px",
    width: "32px",
    ...shorthands.padding("0"),
  },
  navSection: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.padding("4px", "8px"),
    ...shorthands.gap("2px"),
    flexShrink: 0,
  },
  sectionHeader: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    ...shorthands.padding("8px", "12px", "4px"),
    flexShrink: 0,
  },
  sectionTitle: {
    fontSize: "11px",
    fontWeight: 600 as unknown as string,
    textTransform: "uppercase" as const,
    letterSpacing: "0.5px",
    color: tokens.colorNeutralForeground3,
  },
  chatSection: {
    display: "flex",
    flexDirection: "column",
    flexGrow: 1,
    minHeight: 0,
    overflowY: "hidden",
  },
  collapsedChats: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    ...shorthands.gap("4px"),
    ...shorthands.padding("8px", "0"),
  },
  bottomSection: {
    display: "flex",
    alignItems: "center",
    ...shorthands.padding("8px"),
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke2),
    flexShrink: 0,
    ...shorthands.gap("8px"),
  },
  userAvatar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    width: "28px",
    height: "28px",
    ...shorthands.borderRadius("50%"),
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    flexShrink: 0,
    fontSize: "12px",
    fontWeight: 600 as unknown as string,
  },
  projectDropdown: {
    minWidth: "140px",
    width: "100%",
  },
});

export function Sidebar({
  isCollapsed,
  onToggleCollapse,
  onNewChat,
  onLoadSession,
  currentSessionId,
  refreshTrigger,
}: SidebarProps): React.ReactElement {
  const styles = useStyles();

  const sidebarWidth = isCollapsed ? SIDEBAR_COLLAPSED_WIDTH : SIDEBAR_WIDTH;

  return (
    <div className={styles.sidebar} style={{ width: sidebarWidth }}>
      {/* New chat button */}
      <div className={styles.topSection}>
        {isCollapsed ? (
          <Tooltip content="New chat" relationship="label" positioning="after">
            <Button
              className={styles.newChatButtonCollapsed}
              appearance="subtle"
              icon={<AddRegular />}
              size="small"
              onClick={onNewChat}
            />
          </Tooltip>
        ) : (
          <Button
            className={styles.newChatButton}
            appearance="subtle"
            icon={<AddRegular />}
            size="small"
            onClick={onNewChat}
          >
            New chat
          </Button>
        )}
      </div>

      {/* Chats section */}
      {!isCollapsed ? (
        <>
          <div className={styles.sectionHeader}>
            <span className={styles.sectionTitle}>Chats</span>
          </div>
          <div className={styles.chatSection}>
            <SessionList
              onLoadSession={onLoadSession}
              currentSessionId={currentSessionId}
              refreshTrigger={refreshTrigger}
              compact
            />
          </div>
        </>
      ) : (
        <div className={styles.collapsedChats}>
          <Tooltip
            content="Chat history"
            relationship="label"
            positioning="after"
          >
            <Button
              appearance="subtle"
              icon={<ChatRegular />}
              size="small"
              onClick={onToggleCollapse}
            />
          </Tooltip>
        </div>
      )}

      {/* Bottom: collapse toggle */}
      <div className={styles.bottomSection}>
        {!isCollapsed && (
          <div className={styles.userAvatar}>
            <PersonRegular fontSize={14} />
          </div>
        )}
        {!isCollapsed && <div style={{ flexGrow: 1 }} />}
        <Tooltip
          content={isCollapsed ? "Expand sidebar" : "Collapse sidebar"}
          relationship="label"
          positioning={isCollapsed ? "after" : "above"}
        >
          <Button
            appearance="subtle"
            icon={
              isCollapsed ? <PanelLeftExpandRegular /> : <PanelLeftRegular />
            }
            size="small"
            onClick={onToggleCollapse}
          />
        </Tooltip>
      </div>
    </div>
  );
}
