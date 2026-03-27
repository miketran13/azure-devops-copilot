import * as React from "react";
import {
  Button,
  Text,
  makeStyles,
  tokens,
  shorthands,
} from "@fluentui/react-components";
import {
  DismissRegular,
  ClipboardTaskRegular,
  FolderRegular,
  AttachRegular,
  PersonRegular,
  BranchRegular,
} from "@fluentui/react-icons";

export type AttachmentType =
  | "workItem"
  | "project"
  | "file"
  | "repository"
  | "member";

export interface AttachmentItem {
  type: AttachmentType;
  id: string;
  label: string;
  secondaryLabel?: string;
  /** Text content for file attachments — included inline when sending to the AI */
  content?: string;
}

interface AttachmentChipProps {
  attachment: AttachmentItem;
  onRemove: (id: string) => void;
}

const TYPE_ICONS: Record<AttachmentType, React.ReactElement> = {
  workItem: <ClipboardTaskRegular fontSize={12} />,
  project: <FolderRegular fontSize={12} />,
  file: <AttachRegular fontSize={12} />,
  repository: <BranchRegular fontSize={12} />,
  member: <PersonRegular fontSize={12} />,
};

const TYPE_COLORS: Record<AttachmentType, string> = {
  workItem: tokens.colorPaletteBlueBorderActive,
  project: tokens.colorPalettePurpleBorderActive,
  file: tokens.colorPaletteGreenBorderActive,
  repository: tokens.colorPaletteMarigoldBorderActive,
  member: tokens.colorPaletteTealBorderActive,
};

const useStyles = makeStyles({
  chip: {
    display: "inline-flex",
    alignItems: "center",
    ...shorthands.gap("4px"),
    ...shorthands.padding("2px", "4px", "2px", "8px"),
    ...shorthands.borderRadius("12px"),
    ...shorthands.border("1px", "solid", tokens.colorNeutralStroke2),
    backgroundColor: tokens.colorNeutralBackground3,
    fontSize: "12px",
    maxWidth: "200px",
    transitionProperty: "background-color",
    transitionDuration: "0.1s",
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground3Hover,
    },
  },
  label: {
    ...shorthands.overflow("hidden"),
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  removeBtn: {
    minWidth: "16px",
    width: "16px",
    height: "16px",
    ...shorthands.padding("0"),
  },
});

export function AttachmentChip({
  attachment,
  onRemove,
}: AttachmentChipProps): React.ReactElement {
  const styles = useStyles();
  const icon = TYPE_ICONS[attachment.type];

  return (
    <span className={styles.chip}>
      {icon}
      <span className={styles.label}>{attachment.label}</span>
      <Button
        className={styles.removeBtn}
        appearance="transparent"
        size="small"
        icon={<DismissRegular fontSize={10} />}
        onClick={() => onRemove(attachment.id)}
        title="Remove"
      />
    </span>
  );
}
