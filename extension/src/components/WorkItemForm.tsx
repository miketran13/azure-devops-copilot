import * as React from "react";
import {
  Input,
  Dropdown,
  Option,
  Button,
  Text,
  Divider,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import { MarkdownEditor } from "./MarkdownEditor";
import { FieldPicker } from "./FieldPicker";
import type { FieldOption } from "./FieldPicker";
import type { WorkItemFormData } from "../hooks/usePreviewPanel";

/** Work item type definitions with their type-specific fields */
const WORK_ITEM_TYPES = [
  "Bug",
  "Task",
  "User Story",
  "Feature",
  "Epic",
  "Test Case",
  "Issue",
];

const PRIORITY_OPTIONS: FieldOption[] = [
  { value: "1", label: "1 - Critical" },
  { value: "2", label: "2 - High" },
  { value: "3", label: "3 - Medium" },
  { value: "4", label: "4 - Low" },
];

const STATE_OPTIONS: Record<string, FieldOption[]> = {
  default: [
    { value: "New", label: "New" },
    { value: "Active", label: "Active" },
    { value: "Resolved", label: "Resolved" },
    { value: "Closed", label: "Closed" },
  ],
  Bug: [
    { value: "New", label: "New" },
    { value: "Active", label: "Active" },
    { value: "Resolved", label: "Resolved" },
    { value: "Closed", label: "Closed" },
  ],
  "User Story": [
    { value: "New", label: "New" },
    { value: "Active", label: "Active" },
    { value: "Resolved", label: "Resolved" },
    { value: "Closed", label: "Closed" },
  ],
  Task: [
    { value: "New", label: "New" },
    { value: "Active", label: "Active" },
    { value: "Closed", label: "Closed" },
  ],
};

const TYPE_COLORS: Record<string, string> = {
  Bug: "#cc293d",
  "User Story": "#009ccc",
  Task: "#f2cb1d",
  Feature: "#773b93",
  Epic: "#ff7b00",
  "Test Case": "#004b50",
  Issue: "#cc293d",
};

export interface WorkItemFormProps {
  data: WorkItemFormData;
  onChange: (data: Partial<WorkItemFormData>) => void;
  loadAreaPaths?: () => Promise<FieldOption[]>;
  loadIterationPaths?: () => Promise<FieldOption[]>;
  loadTeamMembers?: () => Promise<FieldOption[]>;
  editingId?: number;
}

const useStyles = makeStyles({
  form: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("16px"),
    ...shorthands.padding("16px"),
  },
  typeSelector: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
  },
  typeBar: {
    width: "4px",
    height: "28px",
    ...shorthands.borderRadius("2px"),
    flexShrink: 0,
  },
  typeDropdown: {
    flexGrow: 1,
  },
  titleInput: {
    width: "100%",
    "& input": {
      fontSize: "16px",
      fontWeight: 600 as unknown as string,
    },
  },
  section: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("8px"),
  },
  sectionTitle: {
    fontSize: "12px",
    fontWeight: 600 as unknown as string,
    textTransform: "uppercase" as const,
    letterSpacing: "0.5px",
    color: tokens.colorNeutralForeground3,
  },
  fieldGrid: {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    ...shorthands.gap("12px"),
  },
  fullWidth: {
    gridColumn: "1 / -1",
  },
  metaBar: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("8px"),
    ...shorthands.padding("8px", "0"),
    ...shorthands.borderTop("1px", "solid", tokens.colorNeutralStroke2),
  },
});

export function WorkItemForm({
  data,
  onChange,
  loadAreaPaths,
  loadIterationPaths,
  loadTeamMembers,
  editingId,
}: WorkItemFormProps): React.ReactElement {
  const styles = useStyles();
  const typeColor = TYPE_COLORS[data.workItemType] ?? "#0078d4";
  const isBug = /bug|issue|defect/i.test(data.workItemType);
  const isStory = /user story|story/i.test(data.workItemType);
  const hasStoryPoints = isStory || /feature/i.test(data.workItemType);

  const stateOptions =
    STATE_OPTIONS[data.workItemType] ?? STATE_OPTIONS.default;

  return (
    <div className={styles.form}>
      {/* Work Item Type + ID */}
      <div className={styles.typeSelector}>
        <div
          className={styles.typeBar}
          style={{ backgroundColor: typeColor }}
        />
        <Dropdown
          className={styles.typeDropdown}
          size="small"
          value={data.workItemType || "Select type"}
          selectedOptions={data.workItemType ? [data.workItemType] : []}
          onOptionSelect={(_e, d) => {
            if (d.optionValue) onChange({ workItemType: d.optionValue });
          }}
        >
          {WORK_ITEM_TYPES.map((t) => (
            <Option key={t} value={t} text={t}>
              <div
                style={{ display: "flex", alignItems: "center", gap: "8px" }}
              >
                <div
                  style={{
                    width: "8px",
                    height: "8px",
                    borderRadius: "50%",
                    backgroundColor: TYPE_COLORS[t] ?? "#0078d4",
                  }}
                />
                {t}
              </div>
            </Option>
          ))}
        </Dropdown>
        {editingId && (
          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
            #{editingId}
          </Text>
        )}
      </div>

      {/* Title */}
      <Input
        className={styles.titleInput}
        value={data.title}
        onChange={(_e, d) => onChange({ title: d.value })}
        placeholder="Add a title"
        size="large"
      />

      {/* Description */}
      <div className={styles.section}>
        <span className={styles.sectionTitle}>Description</span>
        <MarkdownEditor
          value={data.description ?? ""}
          onChange={(v) => onChange({ description: v })}
          placeholder="Add a description..."
          minRows={4}
        />
      </div>

      {/* Acceptance Criteria (User Story) */}
      {isStory && (
        <div className={styles.section}>
          <span className={styles.sectionTitle}>Acceptance Criteria</span>
          <MarkdownEditor
            value={data.acceptanceCriteria ?? ""}
            onChange={(v) => onChange({ acceptanceCriteria: v })}
            placeholder="Enter acceptance criteria in Given/When/Then format..."
            minRows={3}
          />
        </div>
      )}

      {/* Repro Steps (Bug) */}
      {isBug && (
        <div className={styles.section}>
          <span className={styles.sectionTitle}>Repro Steps</span>
          <MarkdownEditor
            value={data.reproSteps ?? ""}
            onChange={(v) => onChange({ reproSteps: v })}
            placeholder="Steps to reproduce this bug..."
            minRows={3}
          />
        </div>
      )}

      <Divider />

      {/* Fields grid */}
      <div className={styles.section}>
        <span className={styles.sectionTitle}>Details</span>
        <div className={styles.fieldGrid}>
          <FieldPicker
            mode="dropdown"
            label="State"
            value={data.state ?? "New"}
            onChange={(v) => onChange({ state: v as string })}
            options={stateOptions}
          />

          <FieldPicker
            mode="dropdown"
            label="Priority"
            value={data.priority?.toString() ?? ""}
            onChange={(v) => onChange({ priority: parseInt(v as string, 10) })}
            options={PRIORITY_OPTIONS}
          />

          {loadTeamMembers && (
            <FieldPicker
              mode="search"
              label="Assigned To"
              value={data.assignedTo ?? ""}
              onChange={(v) => onChange({ assignedTo: v as string })}
              loadOptions={loadTeamMembers}
              placeholder="Search team members..."
            />
          )}

          {hasStoryPoints && (
            <div>
              <span
                style={{
                  fontSize: "12px",
                  fontWeight: 600,
                  color: tokens.colorNeutralForeground2,
                  display: "block",
                  marginBottom: "4px",
                }}
              >
                Story Points
              </span>
              <Input
                size="small"
                type="number"
                value={data.storyPoints?.toString() ?? ""}
                onChange={(_e, d) =>
                  onChange({
                    storyPoints: d.value ? parseInt(d.value, 10) : undefined,
                  })
                }
                style={{ width: "100%" }}
              />
            </div>
          )}

          {loadAreaPaths && (
            <div className={styles.fullWidth}>
              <FieldPicker
                mode="search"
                label="Area Path"
                value={data.areaPath ?? ""}
                onChange={(v) => onChange({ areaPath: v as string })}
                loadOptions={loadAreaPaths}
                placeholder="Search area paths..."
              />
            </div>
          )}

          {loadIterationPaths && (
            <div className={styles.fullWidth}>
              <FieldPicker
                mode="search"
                label="Iteration Path"
                value={data.iterationPath ?? ""}
                onChange={(v) => onChange({ iterationPath: v as string })}
                loadOptions={loadIterationPaths}
                placeholder="Search iteration paths..."
              />
            </div>
          )}

          <div className={styles.fullWidth}>
            <FieldPicker
              mode="tags"
              label="Tags"
              value={data.tags ?? ""}
              onChange={(v) =>
                onChange({
                  tags: Array.isArray(v) ? v.join("; ") : v,
                })
              }
              placeholder="Add tags..."
            />
          </div>
        </div>
      </div>

      {/* Relationships */}
      {data.parentId !== undefined && (
        <>
          <Divider />
          <div className={styles.section}>
            <span className={styles.sectionTitle}>Relationships</span>
            <div>
              <span
                style={{
                  fontSize: "12px",
                  fontWeight: 600,
                  color: tokens.colorNeutralForeground2,
                  display: "block",
                  marginBottom: "4px",
                }}
              >
                Parent Work Item
              </span>
              <Input
                size="small"
                type="number"
                value={data.parentId?.toString() ?? ""}
                onChange={(_e, d) =>
                  onChange({
                    parentId: d.value ? parseInt(d.value, 10) : undefined,
                  })
                }
                placeholder="Parent work item ID"
                style={{ width: "100%" }}
              />
            </div>
          </div>
        </>
      )}
    </div>
  );
}
