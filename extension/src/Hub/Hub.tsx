import * as SDK from "azure-devops-extension-sdk";
import * as React from "react";
import { createRoot } from "react-dom/client";
import {
  Spinner,
  Text,
  MessageBar,
  MessageBarBody,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import { FluentThemeProvider } from "../providers/FluentThemeProvider";
import { AppShell } from "../components/AppShell";

import "./Hub.scss";

const useStyles = makeStyles({
  loading: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    height: "100vh",
    ...shorthands.gap("16px"),
  },
  error: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    justifyContent: "center",
    height: "100vh",
    ...shorthands.gap("8px"),
  },
});

/**
 * Hub entry point — the main Copilot chat page under Azure Boards.
 * Uses AppShell for the 3-panel layout (sidebar, chat, preview).
 */
function HubContent(): React.ReactElement {
  const styles = useStyles();
  const [ready, setReady] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();

  React.useEffect(() => {
    SDK.init()
      .then(() => {
        SDK.ready().then(() => setReady(true));
      })
      .catch((err) => {
        setError(`Failed to initialize SDK: ${err.message}`);
      });
  }, []);

  if (error) {
    return (
      <div className={styles.error}>
        <Text size={500} weight="semibold">
          DevOps Copilot
        </Text>
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      </div>
    );
  }

  if (!ready) {
    return (
      <div className={styles.loading}>
        <Spinner size="medium" label="Initializing DevOps Copilot..." />
      </div>
    );
  }

  return <AppShell />;
}

function Hub(): React.ReactElement {
  return (
    <FluentThemeProvider>
      <HubContent />
    </FluentThemeProvider>
  );
}

const root = createRoot(document.getElementById("root")!);
root.render(<Hub />);
