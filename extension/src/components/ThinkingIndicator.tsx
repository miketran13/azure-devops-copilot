import * as React from "react";
import {
  Text,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  CheckmarkCircleRegular,
  ArrowSyncRegular,
  ChevronRightRegular,
  ChevronDownRegular,
  BotSparkleRegular,
} from "@fluentui/react-icons";
import type { ProcessingStep } from "../models/types";

export interface ThinkingIndicatorProps {
  steps: ProcessingStep[];
  isActive: boolean;
}

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.borderRadius("8px"),
    backgroundColor: tokens.colorNeutralBackground3,
    ...shorthands.overflow("hidden"),
    maxWidth: "80%",
  },
  header: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
    ...shorthands.padding("8px", "12px"),
    cursor: "pointer",
    userSelect: "none",
    transitionProperty: "background-color",
    transitionDuration: "0.1s",
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground3Hover,
    },
  },
  headerIcon: {
    color: tokens.colorBrandForeground1,
    flexShrink: 0,
  },
  headerText: {
    flexGrow: 1,
  },
  chevron: {
    color: tokens.colorNeutralForeground3,
    flexShrink: 0,
    transitionProperty: "transform",
    transitionDuration: "0.15s",
  },
  stepsContainer: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("4px"),
    ...shorthands.padding("0", "12px", "8px"),
    animationName: {
      from: { opacity: 0, maxHeight: "0px" },
      to: { opacity: 1, maxHeight: "300px" },
    },
    animationDuration: "0.2s",
    animationTimingFunction: "ease-out",
    animationFillMode: "both",
  },
  stepItem: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("6px"),
    fontSize: "12px",
    color: tokens.colorNeutralForeground3,
  },
  stepItemActive: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("6px"),
    fontSize: "12px",
    color: tokens.colorBrandForeground1,
    fontWeight: 600 as unknown as string,
  },
  stepItemDone: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("6px"),
    fontSize: "12px",
    color: tokens.colorNeutralForeground3,
    opacity: 0.7,
  },
});

export function ThinkingIndicator({
  steps,
  isActive,
}: ThinkingIndicatorProps): React.ReactElement {
  const styles = useStyles();
  const [expanded, setExpanded] = React.useState(true);

  // Auto-collapse when done
  React.useEffect(() => {
    if (!isActive && steps.length > 0) {
      const timer = setTimeout(() => setExpanded(false), 1000);
      return () => clearTimeout(timer);
    }
  }, [isActive, steps.length]);

  // Auto-expand when new steps come in while active
  React.useEffect(() => {
    if (isActive) {
      setExpanded(true);
    }
  }, [isActive]);

  if (steps.length === 0 && !isActive) return <></>;

  return (
    <div className={styles.container}>
      <div
        className={styles.header}
        onClick={() => setExpanded(!expanded)}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === "Enter" && setExpanded(!expanded)}
      >
        <BotSparkleRegular fontSize={16} className={styles.headerIcon} />
        <Text size={200} weight="semibold" className={styles.headerText}>
          {isActive ? "Copilot is thinking..." : "Copilot finished thinking"}
        </Text>
        {expanded ? (
          <ChevronDownRegular fontSize={12} className={styles.chevron} />
        ) : (
          <ChevronRightRegular fontSize={12} className={styles.chevron} />
        )}
      </div>
      {expanded && steps.length > 0 && (
        <div className={styles.stepsContainer}>
          {steps.map((step) => {
            const className =
              step.status === "active"
                ? styles.stepItemActive
                : styles.stepItemDone;
            return (
              <div key={step.id} className={className}>
                {step.status === "active" ? (
                  <ArrowSyncRegular fontSize={12} className="spin-icon" />
                ) : (
                  <CheckmarkCircleRegular fontSize={12} />
                )}
                <span>{step.label}</span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
