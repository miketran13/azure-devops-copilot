import * as SDK from "azure-devops-extension-sdk";
import * as React from "react";
import * as ReactDOM from "react-dom";
import { ChatPanel } from "../components/ChatPanel";

import "./Hub.scss";

/**
 * Hub entry point — the main Copilot chat page under Azure Boards.
 */
function Hub(): React.ReactElement {
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
      <div className="hub-error">
        <h2>DevOps Copilot</h2>
        <p className="error-text">{error}</p>
      </div>
    );
  }

  if (!ready) {
    return (
      <div className="hub-loading">
        <div className="spinner" />
        <p>Initializing DevOps Copilot...</p>
      </div>
    );
  }

  return (
    <div className="hub-container">
      <div className="hub-header">
        <h1 className="hub-title">DevOps Copilot</h1>
        <p className="hub-subtitle">
          AI-powered assistant for Azure Boards — search, create, analyze, and
          manage work items using natural language.
        </p>
      </div>
      <div className="hub-content">
        <ChatPanel />
      </div>
    </div>
  );
}

ReactDOM.render(<Hub />, document.getElementById("root"));
