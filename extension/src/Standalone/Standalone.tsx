import * as React from "react";
import { createRoot } from "react-dom/client";
import { FluentThemeProvider } from "../providers/FluentThemeProvider";
import { StandaloneShell } from "./StandaloneShell";

import "./Standalone.scss";

function Standalone(): React.ReactElement {
  return (
    <FluentThemeProvider>
      <StandaloneShell />
    </FluentThemeProvider>
  );
}

const root = createRoot(document.getElementById("root")!);
root.render(<Standalone />);
