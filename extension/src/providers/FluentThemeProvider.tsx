import * as React from "react";
import {
  FluentProvider,
  webLightTheme,
  webDarkTheme,
  type Theme,
} from "@fluentui/react-components";

/**
 * Detects the current Azure DevOps theme and wraps children
 * in FluentProvider with the matching FluentUI v9 theme.
 */
export function FluentThemeProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [theme, setTheme] = React.useState<Theme>(webLightTheme);

  React.useEffect(() => {
    // Detect ADO theme from the body's data-theme attribute or CSS variables
    const detectTheme = () => {
      const body = document.body;
      const dataTheme = body.getAttribute("data-theme");
      const bgColor =
        getComputedStyle(body).getPropertyValue("--background-color");

      if (
        dataTheme?.includes("dark") ||
        bgColor?.trim().startsWith("#1e") ||
        bgColor?.trim().startsWith("#2d") ||
        bgColor?.trim().startsWith("#333")
      ) {
        setTheme(webDarkTheme);
      } else {
        setTheme(webLightTheme);
      }
    };

    detectTheme();

    // Watch for theme changes via MutationObserver on body attributes
    const observer = new MutationObserver(detectTheme);
    observer.observe(document.body, {
      attributes: true,
      attributeFilter: ["data-theme", "class"],
    });

    return () => observer.disconnect();
  }, []);

  return (
    <FluentProvider theme={theme} style={{ height: "100%" }}>
      {children}
    </FluentProvider>
  );
}
