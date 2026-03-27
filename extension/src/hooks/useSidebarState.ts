import * as React from "react";

const STORAGE_KEY = "devops-copilot-sidebar-collapsed";

export interface UseSidebarStateReturn {
  isCollapsed: boolean;
  toggle: () => void;
  expand: () => void;
  collapse: () => void;
}

export function useSidebarState(): UseSidebarStateReturn {
  const [isCollapsed, setIsCollapsed] = React.useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === "true";
    } catch {
      return false;
    }
  });

  const toggle = React.useCallback(() => {
    setIsCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem(STORAGE_KEY, String(next));
      } catch {
        /* noop */
      }
      return next;
    });
  }, []);

  const expand = React.useCallback(() => {
    setIsCollapsed(false);
    try {
      localStorage.setItem(STORAGE_KEY, "false");
    } catch {
      /* noop */
    }
  }, []);

  const collapse = React.useCallback(() => {
    setIsCollapsed(true);
    try {
      localStorage.setItem(STORAGE_KEY, "true");
    } catch {
      /* noop */
    }
  }, []);

  return { isCollapsed, toggle, expand, collapse };
}
