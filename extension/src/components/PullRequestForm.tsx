import * as React from "react";
import {
  Input,
  Dropdown,
  Option,
  Switch,
  Text,
  Divider,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import { MarkdownEditor } from "./MarkdownEditor";
import { FieldPicker } from "./FieldPicker";
import type { FieldOption } from "./FieldPicker";
import type { PullRequestFormData } from "../hooks/usePreviewPanel";

export interface PullRequestFormProps {
  data: PullRequestFormData;
  onChange: (data: Partial<PullRequestFormData>) => void;
  loadRepositories?: () => Promise<FieldOption[]>;
  loadBranches?: (repo: string) => Promise<FieldOption[]>;
  editingId?: number;
}

const useStyles = makeStyles({
  form: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("16px"),
    ...shorthands.padding("16px"),
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
  branchRow: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
  },
  branchDropdown: {
    flexGrow: 1,
  },
  arrow: {
    color: tokens.colorNeutralForeground3,
    fontSize: "14px",
    flexShrink: 0,
  },
  fieldLabel: {
    fontSize: "12px",
    fontWeight: 600 as unknown as string,
    color: tokens.colorNeutralForeground2,
    display: "block",
    marginBottom: "4px",
  },
  draftSwitch: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap("8px"),
  },
});

export function PullRequestForm({
  data,
  onChange,
  loadRepositories,
  loadBranches,
  editingId,
}: PullRequestFormProps): React.ReactElement {
  const styles = useStyles();
  const [branches, setBranches] = React.useState<FieldOption[]>([]);

  // Load branches when repo changes
  React.useEffect(() => {
    if (data.repositoryName && loadBranches) {
      loadBranches(data.repositoryName)
        .then(setBranches)
        .catch(() => setBranches([]));
    }
  }, [data.repositoryName, loadBranches]);

  return (
    <div className={styles.form}>
      {/* Repository */}
      {loadRepositories && (
        <FieldPicker
          mode="search"
          label="Repository"
          value={data.repositoryName}
          onChange={(v) => onChange({ repositoryName: v as string })}
          loadOptions={loadRepositories}
          placeholder="Select repository..."
          required
        />
      )}

      {/* Branch selectors */}
      <div className={styles.section}>
        <span className={styles.sectionTitle}>Branches</span>
        <div className={styles.branchRow}>
          <Dropdown
            className={styles.branchDropdown}
            size="small"
            value={data.sourceBranch || "Source branch"}
            selectedOptions={data.sourceBranch ? [data.sourceBranch] : []}
            onOptionSelect={(_e, d) => {
              if (d.optionValue) onChange({ sourceBranch: d.optionValue });
            }}
          >
            {branches.map((b) => (
              <Option key={b.value} value={b.value} text={b.label}>
                {b.label}
              </Option>
            ))}
          </Dropdown>
          <span className={styles.arrow}>→</span>
          <Dropdown
            className={styles.branchDropdown}
            size="small"
            value={data.targetBranch || "Target branch"}
            selectedOptions={data.targetBranch ? [data.targetBranch] : []}
            onOptionSelect={(_e, d) => {
              if (d.optionValue) onChange({ targetBranch: d.optionValue });
            }}
          >
            {branches.map((b) => (
              <Option key={b.value} value={b.value} text={b.label}>
                {b.label}
              </Option>
            ))}
          </Dropdown>
        </div>
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
          placeholder="Describe the changes in this pull request..."
          minRows={4}
        />
      </div>

      <Divider />

      {/* Linked work items */}
      <div className={styles.section}>
        <span className={styles.fieldLabel}>Linked Work Items</span>
        <FieldPicker
          mode="tags"
          label=""
          value={data.workItemIds?.map((id) => `#${id}`) ?? []}
          onChange={(v) => {
            const ids = (Array.isArray(v) ? v : [v])
              .map((s) => parseInt(s.replace("#", ""), 10))
              .filter((n) => !isNaN(n));
            onChange({ workItemIds: ids });
          }}
          placeholder="Type work item ID and press Enter"
        />
      </div>

      {/* Draft toggle */}
      <div className={styles.draftSwitch}>
        <Switch
          checked={data.isDraft ?? false}
          onChange={(_e, d) => onChange({ isDraft: d.checked })}
          label="Create as draft"
        />
      </div>
    </div>
  );
}
