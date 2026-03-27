import * as React from "react";
import type { WorkItemSummary } from "../models/types";

export type PreviewPanelType = "workItem" | "pullRequest" | null;

export interface WorkItemFormData {
  workItemType: string;
  title: string;
  description?: string;
  state?: string;
  assignedTo?: string;
  areaPath?: string;
  iterationPath?: string;
  priority?: number;
  storyPoints?: number;
  tags?: string;
  valueArea?: string;
  acceptanceCriteria?: string;
  reproSteps?: string;
  parentId?: number;
  customFields?: Record<string, unknown>;
}

export interface PullRequestFormData {
  repositoryName: string;
  sourceBranch: string;
  targetBranch: string;
  title: string;
  description?: string;
  workItemIds?: number[];
  isDraft?: boolean;
}

export type PreviewPanelData = WorkItemFormData | PullRequestFormData;

export interface PreviewPanelState {
  isOpen: boolean;
  panelType: PreviewPanelType;
  panelData: PreviewPanelData | null;
  /** If editing an existing item, its ID */
  editingId?: number;
}

export interface UsePreviewPanelReturn extends PreviewPanelState {
  openPreview: (
    type: PreviewPanelType,
    data?: PreviewPanelData,
    editingId?: number,
  ) => void;
  closePreview: () => void;
  updatePreviewData: (partial: Partial<PreviewPanelData>) => void;
}

export function usePreviewPanel(): UsePreviewPanelReturn {
  const [state, setState] = React.useState<PreviewPanelState>({
    isOpen: false,
    panelType: null,
    panelData: null,
  });

  const openPreview = React.useCallback(
    (type: PreviewPanelType, data?: PreviewPanelData, editingId?: number) => {
      setState({
        isOpen: true,
        panelType: type,
        panelData: data ?? null,
        editingId,
      });
    },
    [],
  );

  const closePreview = React.useCallback(() => {
    setState({
      isOpen: false,
      panelType: null,
      panelData: null,
      editingId: undefined,
    });
  }, []);

  const updatePreviewData = React.useCallback(
    (partial: Partial<PreviewPanelData>) => {
      setState((prev) => ({
        ...prev,
        panelData: prev.panelData
          ? { ...prev.panelData, ...partial }
          : (partial as PreviewPanelData),
      }));
    },
    [],
  );

  return {
    ...state,
    openPreview,
    closePreview,
    updatePreviewData,
  };
}
