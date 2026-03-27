import * as SDK from "azure-devops-extension-sdk";
import * as React from "react";
import { createRoot } from "react-dom/client";
import {
  makeStyles,
  tokens,
  shorthands,
  Spinner,
} from "@fluentui/react-components";
import { FluentThemeProvider } from "../providers/FluentThemeProvider";
import { ChatPanel } from "../components/ChatPanel";
import type { ChatPanelProps } from "../components/ChatPanel";

import "./GlobalChat.scss";

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    height: "100%",
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
  },
  loading: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    height: "100%",
  },
});

/**
 * Global chat panel content — rendered inside an ADO slide-out panel
 * opened via IHostPageLayoutService.openPanel().
 */
function GlobalChatContent(props: ChatPanelProps): React.ReactElement {
  const styles = useStyles();

  return (
    <div className={styles.container}>
      <ChatPanel
        initialMessage={props.initialMessage}
        workItemContext={props.workItemContext}
      />
    </div>
  );
}

function GlobalChat(): React.ReactElement {
  const styles = useStyles();
  const [sdkReady, setSdkReady] = React.useState(false);
  const [panelConfig, setPanelConfig] = React.useState<ChatPanelProps>({});
  const [timedOut, setTimedOut] = React.useState(false);

  // Timeout: render the chat panel regardless after 2 seconds
  React.useEffect(() => {
    const timer = setTimeout(() => setTimedOut(true), 2000);
    return () => clearTimeout(timer);
  }, []);

  React.useEffect(() => {
    SDK.init({ loaded: true })
      .then(() => {
        SDK.register(SDK.getContributionId(), () => {
          return {};
        });
        return SDK.ready();
      })
      .then(() => {
        // Read configuration passed by openChatPanel()
        const config = SDK.getConfiguration();
        const chatProps: ChatPanelProps = {};

        if (config?.initialMessage) {
          chatProps.initialMessage = config.initialMessage;
        }
        if (config?.workItemContext) {
          chatProps.workItemContext = config.workItemContext;
        }

        setPanelConfig(chatProps);
        setSdkReady(true);
      })
      .catch((err) => {
        console.warn(
          "GlobalChat: SDK init failed, rendering without config",
          err,
        );
        setSdkReady(true); // Render anyway — just without WI context
      });
  }, []);

  const shouldRender = sdkReady || timedOut;

  if (!shouldRender) {
    return (
      <FluentThemeProvider>
        <div className={styles.loading}>
          <Spinner size="small" label="Loading Copilot..." />
        </div>
      </FluentThemeProvider>
    );
  }

  return (
    <FluentThemeProvider>
      <GlobalChatContent {...panelConfig} />
    </FluentThemeProvider>
  );
}

const root = createRoot(document.getElementById("root")!);
root.render(<GlobalChat />);
