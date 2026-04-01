import * as React from "react";
import {
  webLightTheme,
  webDarkTheme,
  type Theme,
} from "@fluentui/react-components";

export type StandaloneTheme = "normal" | "pacman";

/**
 * React context that carries the active StandaloneTheme.
 * Outside the standalone shell it is null — consumers must guard for null.
 */
export const StandaloneThemeContext =
  React.createContext<StandaloneTheme | null>(null);

/** Returns the current standalone theme, or null when outside standalone. */
export function useStandaloneTheme(): StandaloneTheme | null {
  return React.useContext(StandaloneThemeContext);
}

const THEME_STORAGE_KEY = "devops-copilot-standalone-theme";

export function getSavedTheme(): StandaloneTheme {
  const saved = localStorage.getItem(THEME_STORAGE_KEY);
  return saved === "pacman" ? "pacman" : "normal";
}

export function saveTheme(theme: StandaloneTheme): void {
  localStorage.setItem(THEME_STORAGE_KEY, theme);
}

/** Detect system dark-mode preference for the Normal theme */
function systemPrefersDark(): boolean {
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;
}

export function getNormalFluentTheme(): Theme {
  return systemPrefersDark() ? webDarkTheme : webLightTheme;
}

/**
 * Custom Fluent UI theme inspired by the Pac-Man arcade palette.
 * Built on top of webDarkTheme with overrides for the yellow/blue/cyan palette.
 */
export const pacmanFluentTheme: Theme = {
  ...webDarkTheme,

  // Shell / page backgrounds
  colorNeutralBackground1: "#0a0a1a", // deep space black
  colorNeutralBackground2: "#0d0d26",
  colorNeutralBackground3: "#1a1a2e", // AI message bubble
  colorNeutralBackground4: "#12122a",
  colorNeutralBackground5: "#0d0d1e",
  colorNeutralBackground6: "#08081a",
  colorNeutralBackground1Hover: "#141432",
  colorNeutralBackground1Pressed: "#1a1a3e",
  colorNeutralBackground3Hover: "#252545",
  colorNeutralBackground3Pressed: "#1a1a3e",

  // Borders
  colorNeutralStroke1: "#2121de", // arcade blue
  colorNeutralStroke2: "#1a1a6e",
  colorNeutralStroke3: "#141450",
  colorNeutralStrokeAccessible: "#5555ff",
  colorNeutralStrokeOnBrand2: "#2121de",

  // Primary foreground (text)
  colorNeutralForeground1: "#e0e0f0",
  colorNeutralForeground1Hover: "#ffffff",
  colorNeutralForeground2: "#66d4f0", // cyan
  colorNeutralForeground3: "#5577aa",
  colorNeutralForeground4: "#446688",
  colorNeutralForegroundDisabled: "#334466",
  colorNeutralForegroundInverted: "#0a0a1a",

  // Brand = yellow (pac-man)
  colorBrandBackground: "#ffff00", // user message bubble
  colorBrandBackgroundHover: "#ffee00",
  colorBrandBackgroundPressed: "#ddcc00",
  colorBrandBackgroundSelected: "#ffff44",
  colorBrandBackground2: "#2a2a00", // bot avatar bg
  colorBrandBackground2Hover: "#333300",
  colorBrandForeground1: "#ffff00",
  colorBrandForeground2: "#66d4f0", // bot message text
  colorBrandForeground2Hover: "#88e8ff",
  colorNeutralForegroundOnBrand: "#000000", // text on yellow bubbles

  // Input backgrounds
  colorNeutralBackgroundInverted: "#0a0a1a",
  colorSubtleBackground: "#0a0a1a",
  colorSubtleBackgroundHover: "#141432",
  colorSubtleBackgroundPressed: "#1a1a3e",
  colorSubtleBackgroundSelected: "#141432",

  // Scrollbar / shadow
  colorNeutralShadowAmbient: "rgba(33,33,222,0.3)",
  colorNeutralShadowKey: "rgba(33,33,222,0.4)",

  // Compound button / card
  colorNeutralCardBackground: "#1a1a2e",
  colorNeutralCardBackgroundHover: "#21213e",
  colorNeutralCardBackgroundPressed: "#1a1a3e",
};
