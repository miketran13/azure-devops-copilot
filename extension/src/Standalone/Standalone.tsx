import * as React from "react";
import { createRoot } from "react-dom/client";
import { StandaloneShell } from "./StandaloneShell";

import "./Standalone.scss";

// StandaloneShell manages its own FluentProvider so it can swap between themes.
// The ADO extension pages continue to use FluentThemeProvider independently.
const root = createRoot(document.getElementById("root")!);
root.render(<StandaloneShell />);
