import * as React from "react";
import {
  Button,
  Text,
  Spinner,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  DismissRegular,
  CheckmarkRegular,
  AddRegular,
  EditRegular,
} from "@fluentui/react-icons";
import { WorkItemForm } from "./WorkItemForm";
import { PullRequestForm } from "./PullRequestForm";
import type {
  PreviewPanelType,
  PreviewPanelData,
  WorkItemFormData,
  PullRequestFormData,
} from "../hooks/usePreviewPanel";

export interface PreviewPanelProps {
  isOpen: boolean;
  panelType: PreviewPanelType;
  panelData: PreviewPanelData | null;
  editingId?: number;
  onClose: () => void;
  onDataChange: (partial: Partial<PreviewPanelData>) => void;
  onSubmit: () => void;
  isSubmitting?: boolean;
}

const PANEL_WIDTH = 420;

const useStyles = makeStyles({
  overlay: {
    position: "relative",
    width: `${PANEL_WIDTH}px`,
    height: "100%",
    flexShrink: 0,
    display: "flex",
    flexDirection: "column",
    backgroundColor: tokens.colorNeutralBackground1,
    ...shorthands.borderLeft("1px", "solid", tokens.colorNeutralStroke2),
    animationName: {
      from: { transform: `translateX(${PANEL_WIDTH}px)`, opacity: 0 },
      to: { transform: "translateX(0)", opacity: 1 },
    },
    animationDuration: "0.25s",
    animationTimingFunction: "ease-out",
    animationFillMode: "both",
  },
  header: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    ...shorthands.padding("12px", "16px"),
    ...shorthands.borderBottom("1px", "solid", tokens.colorNeutralStroke2),
    flexShrink: 0,
  },
  headerTitle: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
  },
  content: {
    flexGrow: 1,
    overflowY: "auto",
  },
  footer: {
    display: "flex",
    justifyContent: "flex-end",
    ...shorthands.gap("8px"),
    ...shorthands.padding("12px", "16px"),
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke2),
    flexShrink: 0,
    backgroundColor: tokens.colorNeutralBackground1,
  },
});

export function PreviewPanel({
  isOpen,
  panelType,
  panelData,
  editingId,
  onClose,
  onDataChange,
  onSubmit,
  isSubmitting,
}: PreviewPanelProps): React.ReactElement | null {
  const styles = useStyles();

  if (!isOpen || !panelType) return null;

  const title =
    panelType === "workItem"
      ? editingId
        ? `Edit Work Item #${editingId}`
        : "Create Work Item"
      : editingId
        ? `Edit Pull Request #${editingId}`
        : "Create Pull Request";

  const icon = editingId ? (
    <EditRegular fontSize={18} />
  ) : (
    <AddRegular fontSize={18} />
  );

  const submitLabel = editingId ? "Update" : "Create";

  return (
    <div className={styles.overlay}>
      {/* Header */}
      <div className={styles.header}>
        <div className={styles.headerTitle}>
          {icon}
          <Text size={300} weight="semibold">
            {title}
          </Text>
        </div>
        <Button
          appearance="subtle"
          icon={<DismissRegular />}
          size="small"
          onClick={onClose}
          title="Close panel"
        />
      </div>

      {/* Form content */}
      <div className={styles.content}>
        {panelType === "workItem" && panelData && (
          <WorkItemForm
            data={panelData as WorkItemFormData}
            onChange={onDataChange}
            editingId={editingId}
          />
        )}
        {panelType === "pullRequest" && panelData && (
          <PullRequestForm
            data={panelData as PullRequestFormData}
            onChange={onDataChange}
            editingId={editingId}
          />
        )}
      </div>

      {/* Action buttons */}
      <div className={styles.footer}>
        <Button appearance="subtle" onClick={onClose} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button
          appearance="primary"
          icon={isSubmitting ? <Spinner size="tiny" /> : <CheckmarkRegular />}
          onClick={onSubmit}
          disabled={isSubmitting}
        >
          {isSubmitting ? "Saving..." : submitLabel}
        </Button>
      </div>
    </div>
  );
}
