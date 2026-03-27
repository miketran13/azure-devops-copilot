import * as React from "react";
import {
  Popover,
  PopoverTrigger,
  PopoverSurface,
  Button,
  Input,
  Text,
  Spinner,
  makeStyles,
  tokens,
  shorthands,
  Tooltip,
} from "@fluentui/react-components";
import { SearchRegular } from "@fluentui/react-icons";
import type { AttachmentType, AttachmentItem } from "./AttachmentChip";

export interface AttachmentPopoverProps {
  type: AttachmentType;
  icon: React.ReactElement;
  tooltip: string;
  placeholder: string;
  onSelect: (item: AttachmentItem) => void;
  searchFn: (query: string) => Promise<AttachmentItem[]>;
  disabled?: boolean;
}

const useStyles = makeStyles({
  surface: {
    ...shorthands.padding("8px"),
    width: "280px",
    maxHeight: "320px",
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("4px"),
  },
  resultList: {
    display: "flex",
    flexDirection: "column",
    overflowY: "auto",
    maxHeight: "240px",
    ...shorthands.gap("2px"),
  },
  resultItem: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.padding("6px", "8px"),
    ...shorthands.borderRadius("4px"),
    cursor: "pointer",
    transitionProperty: "background-color",
    transitionDuration: "0.1s",
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  resultItemActive: {
    backgroundColor: tokens.colorNeutralBackground1Selected,
  },
  emptyState: {
    ...shorthands.padding("12px"),
    textAlign: "center" as const,
    color: tokens.colorNeutralForeground3,
  },
  triggerButton: {
    minWidth: "28px",
    width: "28px",
    height: "28px",
    ...shorthands.padding("0"),
  },
});

export function AttachmentPopover({
  type,
  icon,
  tooltip,
  placeholder,
  onSelect,
  searchFn,
  disabled,
}: AttachmentPopoverProps): React.ReactElement {
  const styles = useStyles();
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const [results, setResults] = React.useState<AttachmentItem[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [activeIndex, setActiveIndex] = React.useState(-1);
  const debounceRef = React.useRef<ReturnType<typeof setTimeout>>();

  React.useEffect(() => {
    if (!open) {
      setQuery("");
      setResults([]);
      setActiveIndex(-1);
      return;
    }
    // Load initial results on open
    setLoading(true);
    searchFn("")
      .then(setResults)
      .catch(() => setResults([]))
      .finally(() => setLoading(false));
  }, [open, searchFn]);

  const handleQueryChange = (value: string) => {
    setQuery(value);
    setActiveIndex(-1);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(async () => {
      setLoading(true);
      try {
        const items = await searchFn(value);
        setResults(items);
      } catch {
        setResults([]);
      } finally {
        setLoading(false);
      }
    }, 300);
  };

  const handleSelect = (item: AttachmentItem) => {
    onSelect(item);
    setOpen(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((prev) => (prev < results.length - 1 ? prev + 1 : 0));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((prev) => (prev > 0 ? prev - 1 : results.length - 1));
    } else if (e.key === "Enter" && activeIndex >= 0) {
      e.preventDefault();
      handleSelect(results[activeIndex]);
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  };

  return (
    <Popover
      open={open}
      onOpenChange={(_e, data) => setOpen(data.open)}
      positioning="above-start"
      trapFocus
    >
      <PopoverTrigger disableButtonEnhancement>
        <Tooltip content={tooltip} relationship="label">
          <Button
            className={styles.triggerButton}
            appearance="subtle"
            icon={icon}
            size="small"
            disabled={disabled}
          />
        </Tooltip>
      </PopoverTrigger>
      <PopoverSurface className={styles.surface}>
        <Input
          contentBefore={<SearchRegular fontSize={14} />}
          placeholder={placeholder}
          size="small"
          value={query}
          onChange={(_e, data) => handleQueryChange(data.value)}
          onKeyDown={handleKeyDown}
          autoFocus
          style={{ width: "100%" }}
        />
        {loading ? (
          <div className={styles.emptyState}>
            <Spinner size="tiny" />
          </div>
        ) : results.length === 0 ? (
          <div className={styles.emptyState}>
            <Text size={200}>
              {query ? "No results found" : "Type to search..."}
            </Text>
          </div>
        ) : (
          <div className={styles.resultList}>
            {results.map((item, idx) => (
              <div
                key={item.id}
                className={`${styles.resultItem} ${idx === activeIndex ? styles.resultItemActive : ""}`}
                onClick={() => handleSelect(item)}
                onMouseEnter={() => setActiveIndex(idx)}
                role="option"
                aria-selected={idx === activeIndex}
              >
                <Text size={200} weight="semibold" truncate>
                  {item.label}
                </Text>
                {item.secondaryLabel && (
                  <Text
                    size={100}
                    style={{ color: tokens.colorNeutralForeground3 }}
                    truncate
                  >
                    {item.secondaryLabel}
                  </Text>
                )}
              </div>
            ))}
          </div>
        )}
      </PopoverSurface>
    </Popover>
  );
}
