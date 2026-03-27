import * as React from "react";
import {
  Card,
  Text,
  Badge,
  Button,
  Tooltip,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import { OpenRegular } from "@fluentui/react-icons";
import type { WorkItemSummary } from "../models/types";
import "./WorkItemCard.scss";

interface WorkItemCardProps {
  workItem: WorkItemSummary;
  /** If provided, clicking the card body opens the preview panel instead of a new tab */
  onPreview?: (workItem: WorkItemSummary) => void;
}

/** Color mappings for work item types */
const TYPE_COLORS: Record<string, string> = {
  Bug: "#cc293d",
  "User Story": "#009ccc",
  Task: "#f2cb1d",
  Feature: "#773b93",
  Epic: "#ff7b00",
  "Test Case": "#004b50",
  Issue: "#cc293d",
};

/** State → Badge color mapping */
const STATE_BADGE: Record<
  string,
  "informative" | "success" | "warning" | "danger" | "important"
> = {
  New: "informative",
  Active: "warning",
  Resolved: "success",
  Closed: "success",
  Removed: "danger",
};

const useStyles = makeStyles({
  card: {
    cursor: "pointer",
    display: "flex",
    flexDirection: "row",
    ...shorthands.overflow("hidden"),
    transitionProperty: "box-shadow",
    transitionDuration: "0.15s",
    "&:hover": {
      boxShadow: tokens.shadow8,
    },
  },
  typeBar: {
    width: "4px",
    flexShrink: 0,
    ...shorthands.borderRadius("4px", "0", "0", "4px"),
  },
  body: {
    ...shorthands.padding("8px", "12px"),
    flexGrow: 1,
    minWidth: 0,
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: "2px",
  },
  title: {
    ...shorthands.overflow("hidden"),
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
    marginBottom: "4px",
  },
  meta: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
    flexWrap: "wrap",
  },
  tags: {
    display: "flex",
    ...shorthands.gap("4px"),
    flexWrap: "wrap",
    marginTop: "6px",
  },
  tag: {
    fontSize: "11px",
    ...shorthands.padding("1px", "6px"),
    ...shorthands.borderRadius("4px"),
    backgroundColor: tokens.colorNeutralBackground4,
    color: tokens.colorNeutralForeground3,
  },
  openButton: {
    flexShrink: 0,
    alignSelf: "flex-start",
  },
});

/**
 * Renders a compact work item card using FluentUI v9 components.
 */
export function WorkItemCard({
  workItem,
  onPreview,
}: WorkItemCardProps): React.ReactElement {
  const styles = useStyles();
  const typeColor = TYPE_COLORS[workItem.workItemType] ?? "#0078d4";
  const badgeColor = STATE_BADGE[workItem.state] ?? "informative";

  const handleClick = () => {
    if (onPreview) {
      onPreview(workItem);
    } else if (workItem.url) {
      window.open(workItem.url, "_blank");
    }
  };

  const handleOpenExternal = (e: React.MouseEvent | React.KeyboardEvent) => {
    e.stopPropagation();
    if (workItem.url) {
      window.open(workItem.url, "_blank");
    }
  };

  return (
    <Card
      className={styles.card}
      onClick={handleClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e: React.KeyboardEvent) => e.key === "Enter" && handleClick()}
      size="small"
    >
      <div className={styles.typeBar} style={{ backgroundColor: typeColor }} />
      <div className={styles.body}>
        <div className={styles.header}>
          <Text size={100} weight="semibold" style={{ color: typeColor }}>
            {workItem.workItemType}
          </Text>
          <div style={{ display: "flex", alignItems: "center", gap: "4px" }}>
            <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
              #{workItem.id}
            </Text>
            {workItem.url && (
              <Tooltip content="Open in Azure DevOps" relationship="label">
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<OpenRegular fontSize={12} />}
                  className={styles.openButton}
                  onClick={handleOpenExternal}
                  onKeyDown={(e: React.KeyboardEvent) =>
                    e.key === "Enter" && handleOpenExternal(e)
                  }
                  aria-label="Open in Azure DevOps"
                />
              </Tooltip>
            )}
          </div>
        </div>
        <Text className={styles.title} size={200} weight="semibold" block>
          {workItem.title}
        </Text>
        <div className={styles.meta}>
          <Badge appearance="filled" color={badgeColor} size="small">
            {workItem.state}
          </Badge>
          {workItem.assignedTo && (
            <Text size={100} title={workItem.assignedTo} truncate>
              {workItem.assignedTo}
            </Text>
          )}
          {workItem.priority && (
            <Badge
              appearance="outline"
              size="small"
              title={`Priority ${workItem.priority}`}
            >
              P{workItem.priority}
            </Badge>
          )}
        </div>
        {workItem.tags && (
          <div className={styles.tags}>
            {workItem.tags.split(";").map((tag, idx) => (
              <span key={idx} className={styles.tag}>
                {tag.trim()}
              </span>
            ))}
          </div>
        )}
      </div>
    </Card>
  );
}
