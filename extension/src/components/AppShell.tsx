import * as React from "react";
import {
  Text,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import { BotSparkleRegular, FolderRegular } from "@fluentui/react-icons";
import { Sidebar } from "./Sidebar";
import { ChatPanel } from "./ChatPanel";
import { PreviewPanel } from "./PreviewPanel";
import { useSidebarState } from "../hooks/useSidebarState";
import {
  usePreviewPanel,
  type WorkItemFormData,
  type PullRequestFormData,
} from "../hooks/usePreviewPanel";
import { getDevOpsContext } from "../services/devopsContext";
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
  headerSpacer: {
    flexGrow: 1,
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
  brandBar: {
    height: "3px",
    background: `linear-gradient(90deg, ${tokens.colorBrandBackground} 0%, ${tokens.colorBrandBackground2} 100%)`,
    flexShrink: 0,
  },
});

export function AppShell(): React.ReactElement {
  const styles = useStyles();
  const sidebar = useSidebarState();
  const preview = usePreviewPanel();

  // Project state — read from DevOps context (current project only, no selection)
  const [selectedProject, setSelectedProject] = React.useState<string>("");

  // Session loading state for ChatPanel
  const [loadedSession, setLoadedSession] = React.useState<{
    messages: ConversationMessage[];
    sessionId: string;
  } | null>(null);
  const [chatKey, setChatKey] = React.useState(0);
  const [sessionRefreshKey, setSessionRefreshKey] = React.useState(0);

  // Load current project from DevOps context
  React.useEffect(() => {
    getDevOpsContext()
      .then((ctx) => {
        if (ctx.projectName) setSelectedProject(ctx.projectName);
      })
      .catch(() => {
        /* silent */
      });
  }, []);

  const handleNewChat = () => {
    setLoadedSession(null);
    setChatKey((k) => k + 1);
    preview.closePreview();
  };

  const handleLoadSession = (
    messages: ConversationMessage[],
    sessionId: string,
  ) => {
    setLoadedSession({ messages, sessionId });
    setChatKey((k) => k + 1);
  };

  const handlePreviewSubmit = () => {
    // TODO: Wire to backend creation/update tools
    preview.closePreview();
  };

  // Handle preview panel requests from ChatPanel
  const handleOpenPreview = React.useCallback(
    (
      type: "workItem" | "pullRequest",
      data?: WorkItemFormData | PullRequestFormData,
      editingId?: number,
    ) => {
      preview.openPreview(type, data, editingId);
    },
    [preview.openPreview],
  );

  return (
    <div className={styles.shell}>
      {/* Top header bar: brand + project selector */}
      <div className={styles.topHeader}>
        <BotSparkleRegular fontSize={20} className={styles.headerBrandIcon} />
        <Text size={300} weight="semibold" className={styles.headerBrandText}>
          DevOps Copilot
        </Text>
        <div className={styles.headerDivider} />
        <FolderRegular
          fontSize={16}
          style={{ color: tokens.colorNeutralForeground3, flexShrink: 0 }}
        />
        <Text size={200} className={styles.headerProjectText}>
          {selectedProject || "Loading..."}
        </Text>
        <div className={styles.headerSpacer} />
      </div>

      {/* Body: sidebar + chat */}
      <div className={styles.body}>
        {/* Left Sidebar */}
        <Sidebar
          isCollapsed={sidebar.isCollapsed}
          onToggleCollapse={sidebar.toggle}
          onNewChat={handleNewChat}
          onLoadSession={handleLoadSession}
          currentSessionId={loadedSession?.sessionId}
          refreshTrigger={sessionRefreshKey}
        />

        {/* Center Chat */}
        <div className={styles.chatArea}>
          <ChatPanel
            key={chatKey}
            loadedSession={loadedSession ?? undefined}
            selectedProject={selectedProject}
            onOpenPreview={handleOpenPreview}
            showToolbar={false}
            onSessionCreated={() => setSessionRefreshKey((k) => k + 1)}
          />
        </div>

        {/* Right Preview Panel */}
        <PreviewPanel
          isOpen={preview.isOpen}
          panelType={preview.panelType}
          panelData={preview.panelData}
          editingId={preview.editingId}
          onClose={preview.closePreview}
          onDataChange={preview.updatePreviewData}
          onSubmit={handlePreviewSubmit}
        />
      </div>
    </div>
  );
}
