import * as React from "react";
import {
  Dropdown,
  Option,
  Input,
  Tag,
  TagGroup,
  Button,
  Text,
  Spinner,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import { DismissRegular, SearchRegular } from "@fluentui/react-icons";

export type FieldPickerMode = "dropdown" | "search" | "tree" | "tags";

export interface FieldOption {
  value: string;
  label: string;
  children?: FieldOption[];
}

export interface FieldPickerProps {
  mode: FieldPickerMode;
  label: string;
  value: string | string[];
  onChange: (value: string | string[]) => void;
  options?: FieldOption[];
  loadOptions?: () => Promise<FieldOption[]>;
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
}

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("4px"),
  },
  label: {
    fontSize: "12px",
    fontWeight: 600 as unknown as string,
    color: tokens.colorNeutralForeground2,
  },
  required: {
    color: tokens.colorPaletteRedForeground1,
    marginLeft: "2px",
  },
  tagGroup: {
    display: "flex",
    flexWrap: "wrap",
    ...shorthands.gap("4px"),
  },
  tagInput: {
    width: "100%",
    marginTop: "4px",
  },
  treeContainer: {
    maxHeight: "200px",
    overflowY: "auto",
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
    ...shorthands.borderRadius("4px"),
    ...shorthands.padding("4px"),
  },
  treeItem: {
    display: "flex",
    alignItems: "center",
    ...shorthands.padding("4px", "8px"),
    ...shorthands.borderRadius("4px"),
    cursor: "pointer",
    fontSize: "13px",
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  treeItemSelected: {
    backgroundColor: tokens.colorNeutralBackground1Selected,
    fontWeight: 600 as unknown as string,
  },
  treeIndent: {
    width: "16px",
    flexShrink: 0,
  },
  dropdown: {
    width: "100%",
  },
});

export function FieldPicker({
  mode,
  label,
  value,
  onChange,
  options: propOptions,
  loadOptions,
  placeholder,
  disabled,
  required,
}: FieldPickerProps): React.ReactElement {
  const styles = useStyles();
  const [options, setOptions] = React.useState<FieldOption[]>(
    propOptions ?? [],
  );
  const [loading, setLoading] = React.useState(false);
  const [tagInput, setTagInput] = React.useState("");

  React.useEffect(() => {
    if (propOptions) {
      setOptions(propOptions);
    } else if (loadOptions) {
      setLoading(true);
      loadOptions()
        .then(setOptions)
        .catch(() => setOptions([]))
        .finally(() => setLoading(false));
    }
  }, [propOptions, loadOptions]);

  if (loading) {
    return (
      <div className={styles.container}>
        <span className={styles.label}>
          {label}
          {required && <span className={styles.required}>*</span>}
        </span>
        <Spinner size="tiny" />
      </div>
    );
  }

  // Dropdown mode
  if (mode === "dropdown") {
    const strValue = typeof value === "string" ? value : (value[0] ?? "");
    return (
      <div className={styles.container}>
        <span className={styles.label}>
          {label}
          {required && <span className={styles.required}>*</span>}
        </span>
        <Dropdown
          className={styles.dropdown}
          size="small"
          value={strValue || (placeholder ?? `Select ${label.toLowerCase()}`)}
          selectedOptions={strValue ? [strValue] : []}
          onOptionSelect={(_e, data) => {
            if (data.optionValue) onChange(data.optionValue);
          }}
          disabled={disabled}
        >
          {options.map((opt) => (
            <Option key={opt.value} value={opt.value} text={opt.label}>
              {opt.label}
            </Option>
          ))}
        </Dropdown>
      </div>
    );
  }

  // Search mode (flat list with filtering)
  if (mode === "search") {
    const [searchQuery, setSearchQuery] = React.useState("");
    const filtered = searchQuery
      ? options.filter((o) =>
          o.label.toLowerCase().includes(searchQuery.toLowerCase()),
        )
      : options;
    const strValue = typeof value === "string" ? value : (value[0] ?? "");

    return (
      <div className={styles.container}>
        <span className={styles.label}>
          {label}
          {required && <span className={styles.required}>*</span>}
        </span>
        <Input
          contentBefore={<SearchRegular fontSize={14} />}
          size="small"
          value={searchQuery || strValue}
          onChange={(_e, data) => setSearchQuery(data.value)}
          placeholder={placeholder ?? `Search ${label.toLowerCase()}...`}
          disabled={disabled}
          style={{ width: "100%" }}
        />
        {searchQuery && filtered.length > 0 && (
          <div className={styles.treeContainer}>
            {filtered.slice(0, 20).map((opt) => (
              <div
                key={opt.value}
                className={`${styles.treeItem} ${opt.value === strValue ? styles.treeItemSelected : ""}`}
                onClick={() => {
                  onChange(opt.value);
                  setSearchQuery("");
                }}
              >
                {opt.label}
              </div>
            ))}
          </div>
        )}
      </div>
    );
  }

  // Tree mode (for Area/Iteration paths)
  if (mode === "tree") {
    const strValue = typeof value === "string" ? value : (value[0] ?? "");
    const flattenTree = (
      nodes: FieldOption[],
      depth: number = 0,
    ): { option: FieldOption; depth: number }[] => {
      const result: { option: FieldOption; depth: number }[] = [];
      for (const node of nodes) {
        result.push({ option: node, depth });
        if (node.children) {
          result.push(...flattenTree(node.children, depth + 1));
        }
      }
      return result;
    };
    const flatItems = flattenTree(options);

    return (
      <div className={styles.container}>
        <span className={styles.label}>
          {label}
          {required && <span className={styles.required}>*</span>}
        </span>
        <div className={styles.treeContainer}>
          {flatItems.map(({ option, depth }) => (
            <div
              key={option.value}
              className={`${styles.treeItem} ${option.value === strValue ? styles.treeItemSelected : ""}`}
              onClick={() => onChange(option.value)}
              style={{ paddingLeft: `${8 + depth * 16}px` }}
            >
              {option.label}
            </div>
          ))}
        </div>
      </div>
    );
  }

  // Tags mode (multi-select with chips)
  const tags = Array.isArray(value)
    ? value
    : value
      ? value
          .split(";")
          .map((t) => t.trim())
          .filter(Boolean)
      : [];

  const handleAddTag = () => {
    const trimmed = tagInput.trim();
    if (trimmed && !tags.includes(trimmed)) {
      onChange([...tags, trimmed]);
    }
    setTagInput("");
  };

  const handleRemoveTag = (tag: string) => {
    onChange(tags.filter((t) => t !== tag));
  };

  return (
    <div className={styles.container}>
      <span className={styles.label}>
        {label}
        {required && <span className={styles.required}>*</span>}
      </span>
      {tags.length > 0 && (
        <div className={styles.tagGroup}>
          {tags.map((tag) => (
            <Tag
              key={tag}
              size="small"
              dismissible
              dismissIcon={<DismissRegular />}
              value={tag}
              onClick={() => handleRemoveTag(tag)}
            >
              {tag}
            </Tag>
          ))}
        </div>
      )}
      <Input
        className={styles.tagInput}
        size="small"
        value={tagInput}
        onChange={(_e, data) => setTagInput(data.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            handleAddTag();
          }
        }}
        placeholder={placeholder ?? "Type and press Enter to add"}
        disabled={disabled}
      />
    </div>
  );
}
