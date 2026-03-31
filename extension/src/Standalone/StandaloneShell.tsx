import * as React from "react";
import {
  Text,
  makeStyles,
  tokens,
  shorthands,
  MessageBar,
  MessageBarBody,
  Button,
} from "@fluentui/react-components";
import { BotSparkleRegular } from "@fluentui/react-icons";
import { Sidebar } from "../components/Sidebar";
import { ChatPanel } from "../components/ChatPanel";
import { useSidebarState } from "../hooks/useSidebarState";
import {
  isConfigured,
  getStandaloneSettings,
} from "../services/standaloneContext";
import { setBackendUrl, setStandaloneMode } from "../services/backendApi";
import { SettingsButton } from "./Settings";
import type { ConversationMessage } from "../models/types";

const useStyles = makeStyles({
  shell: {
    display: "flex",
    flexDirection: "column",
    height: "100vh",
    ...shorthands.overflow("hidden"),
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
  },
  topHeader: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("12px"),
    ...shorthands.padding("0", "16px"),
    height: "44px",
    flexShrink: 0,
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke2),
  },
  headerBrandIcon: {
    color: tokens.colorBrandForeground1,
    flexShrink: 0,
  },
  headerBrandText: {
    flexShrink: 0,
  },
  headerSpacer: {
    flexGrow: 1,
  },
  headerDivider: {
    height: "18px",
    width: "1px",
    backgroundColor: tokens.colorNeutralStroke2,
    flexShrink: 0,
  },
  headerProjectText: {
    color: tokens.colorNeutralForeground2,
    flexShrink: 0,
  },
  body: {
    display: "flex",
    flexDirection: "row",
    flexGrow: 1,
    minHeight: 0,
    ...shorthands.overflow("hidden"),
  },
  chatArea: {
    flexGrow: 1,
    display: "flex",
    flexDirection: "column",
    minWidth: 0,
    ...shorthands.overflow("hidden"),
  },
  setupBanner: {
    ...shorthands.margin("24px"),
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    flexGrow: 1,
    ...shorthands.gap("16px"),
  },
});

export function StandaloneShell(): React.ReactElement {
  const styles = useStyles();
  const sidebar = useSidebarState();
  const [configured, setConfigured] = React.useState(isConfigured);
  const [chatKey, setChatKey] = React.useState(0);
  const [sessionRefreshKey, setSessionRefreshKey] = React.useState(0);
  const [loadedSession, setLoadedSession] = React.useState<{
    messages: ConversationMessage[];
    sessionId: string;
  } | null>(null);

  // Apply settings on mount and whenever they change
  const applySettings = React.useCallback(() => {
    const s = getStandaloneSettings();
    setBackendUrl(s.backendUrl);
    setStandaloneMode(true, s.githubToken);
    setConfigured(isConfigured());
  }, []);

  React.useEffect(() => {
    applySettings();
  }, [applySettings]);

  const handleNewChat = () => {
    setLoadedSession(null);
    setChatKey((k) => k + 1);
  };

  const handleLoadSession = (
    messages: ConversationMessage[],
    sessionId: string,
  ) => {
    setLoadedSession({ messages, sessionId });
    setChatKey((k) => k + 1);
  };

  const handleSettingsSaved = () => {
    applySettings();
  };

  const settings = getStandaloneSettings();

  return (
    <div className={styles.shell}>
      {/* Top header */}
      <div className={styles.topHeader}>
        <BotSparkleRegular fontSize={20} className={styles.headerBrandIcon} />
        <Text size={300} weight="semibold" className={styles.headerBrandText}>
          DevOps Copilot
        </Text>
        {settings.projectName && (
          <>
            <div className={styles.headerDivider} />
            <Text size={200} className={styles.headerProjectText}>
              {settings.projectName}
            </Text>
          </>
        )}
        <div className={styles.headerSpacer} />
        <SettingsButton onSaved={handleSettingsSaved} />
      </div>

      {/* Body */}
      <div className={styles.body}>
        {configured ? (
          <>
            <Sidebar
              isCollapsed={sidebar.isCollapsed}
              onToggleCollapse={sidebar.toggle}
              onNewChat={handleNewChat}
              onLoadSession={handleLoadSession}
              currentSessionId={loadedSession?.sessionId}
              refreshTrigger={sessionRefreshKey}
            />
            <div className={styles.chatArea}>
              <ChatPanel
                key={chatKey}
                loadedSession={loadedSession ?? undefined}
                selectedProject={settings.projectName}
                showToolbar={false}
                onSessionCreated={() => setSessionRefreshKey((k) => k + 1)}
              />
            </div>
          </>
        ) : (
          <div className={styles.setupBanner}>
            <MessageBar intent="warning">
              <MessageBarBody>
                Configure your GitHub Personal Access Token to get started.
              </MessageBarBody>
            </MessageBar>
            <SettingsButton onSaved={handleSettingsSaved} />
          </div>
        )}
      </div>
    </div>
  );
}
