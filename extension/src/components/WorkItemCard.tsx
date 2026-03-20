import * as React from "react";
import type { WorkItemSummary } from "../models/types";
import "./WorkItemCard.scss";

interface WorkItemCardProps {
  workItem: WorkItemSummary;
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

/** State badge styling */
const STATE_VARIANTS: Record<string, string> = {
  New: "state-new",
  Active: "state-active",
  Resolved: "state-resolved",
  Closed: "state-closed",
  Removed: "state-removed",
};

/**
 * Renders a compact work item card matching Azure DevOps design language.
 */
export function WorkItemCard({
  workItem,
}: WorkItemCardProps): React.ReactElement {
  const typeColor = TYPE_COLORS[workItem.workItemType] ?? "#0078d4";
  const stateClass = STATE_VARIANTS[workItem.state] ?? "state-default";

  const handleClick = () => {
    // Open the work item in Azure DevOps
    if (workItem.url) {
      window.open(workItem.url, "_blank");
    }
  };

  return (
    <div
      className="work-item-card"
      onClick={handleClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === "Enter" && handleClick()}
    >
      <div className="wic-type-bar" style={{ backgroundColor: typeColor }} />
      <div className="wic-body">
        <div className="wic-header">
          <span className="wic-type" style={{ color: typeColor }}>
            {workItem.workItemType}
          </span>
          <span className="wic-id">#{workItem.id}</span>
        </div>
        <div className="wic-title">{workItem.title}</div>
        <div className="wic-meta">
          <span className={`wic-state ${stateClass}`}>{workItem.state}</span>
          {workItem.assignedTo && (
            <span className="wic-assignee" title={workItem.assignedTo}>
              {workItem.assignedTo}
            </span>
          )}
          {workItem.priority && (
            <span
              className="wic-priority"
              title={`Priority ${workItem.priority}`}
            >
              P{workItem.priority}
            </span>
          )}
        </div>
        {workItem.tags && (
          <div className="wic-tags">
            {workItem.tags.split(";").map((tag, idx) => (
              <span key={idx} className="wic-tag">
                {tag.trim()}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
