import * as React from "react";
import {
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
  Input,
  Label,
  Field,
  makeStyles,
  shorthands,
  tokens,
  MessageBar,
  MessageBarBody,
  Spinner,
} from "@fluentui/react-components";
import { SettingsRegular } from "@fluentui/react-icons";
import {
  getStandaloneSettings,
  saveStandaloneSettings,
  type StandaloneSettings,
} from "../services/standaloneContext";

const useStyles = makeStyles({
  form: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("16px"),
  },
  section: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap("8px"),
  },
  sectionTitle: {
    fontWeight: 600,
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground1,
  },
  divider: {
    height: "1px",
    backgroundColor: tokens.colorNeutralStroke2,
    marginTop: "4px",
    marginBottom: "4px",
  },
});

interface SettingsProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: () => void;
}

export function Settings({
  open,
  onOpenChange,
  onSaved,
}: SettingsProps): React.ReactElement {
  const styles = useStyles();
  const [settings, setSettings] = React.useState<StandaloneSettings>(
    getStandaloneSettings,
  );
  const [testing, setTesting] = React.useState(false);
  const [testResult, setTestResult] = React.useState<{
    ok: boolean;
    message: string;
  } | null>(null);

  // Reload settings when dialog opens
  React.useEffect(() => {
    if (open) {
      setSettings(getStandaloneSettings());
      setTestResult(null);
    }
  }, [open]);

  const update = (field: keyof StandaloneSettings, value: string) => {
    setSettings((prev) => ({ ...prev, [field]: value }));
  };

  const handleSave = () => {
    saveStandaloneSettings(settings);
    onSaved?.();
    onOpenChange(false);
  };

  const handleTestConnection = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const baseUrl = settings.backendUrl.replace(/\/$/, "");
      const res = await fetch(`${baseUrl}/health`);
      if (res.ok) {
        setTestResult({
          ok: true,
          message: "Connected to backend successfully.",
        });
      } else {
        setTestResult({
          ok: false,
          message: `Backend returned ${res.status}: ${await res.text()}`,
        });
      }
    } catch (err: unknown) {
      setTestResult({
        ok: false,
        message: `Connection failed: ${err instanceof Error ? err.message : String(err)}`,
      });
    } finally {
      setTesting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(_e, data) => onOpenChange(data.open)}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Settings</DialogTitle>
          <DialogContent>
            <div className={styles.form}>
              {/* Required: GitHub PAT */}
              <div className={styles.section}>
                <span className={styles.sectionTitle}>
                  GitHub Models (required)
                </span>
                <Field label="GitHub Personal Access Token" required>
                  <Input
                    type="password"
                    value={settings.githubToken}
                    onChange={(_e, data) => update("githubToken", data.value)}
                    placeholder="ghp_..."
                  />
                </Field>
              </div>

              <div className={styles.divider} />

              {/* Backend URL */}
              <div className={styles.section}>
                <span className={styles.sectionTitle}>Backend</span>
                <Field label="Backend API URL">
                  <Input
                    value={settings.backendUrl}
                    onChange={(_e, data) => update("backendUrl", data.value)}
                    placeholder="http://localhost:7071/api"
                  />
                </Field>
                <Button
                  size="small"
                  appearance="outline"
                  onClick={handleTestConnection}
                  disabled={testing}
                >
                  {testing ? <Spinner size="tiny" /> : "Test Connection"}
                </Button>
                {testResult && (
                  <MessageBar intent={testResult.ok ? "success" : "error"}>
                    <MessageBarBody>{testResult.message}</MessageBarBody>
                  </MessageBar>
                )}
              </div>

              <div className={styles.divider} />

              {/* Optional: Azure DevOps context */}
              <div className={styles.section}>
                <span className={styles.sectionTitle}>
                  Azure DevOps (optional)
                </span>
                <Field label="Organization URL">
                  <Input
                    value={settings.adoOrgUrl}
                    onChange={(_e, data) => update("adoOrgUrl", data.value)}
                    placeholder="https://dev.azure.com/myorg"
                  />
                </Field>
                <Field label="Personal Access Token">
                  <Input
                    type="password"
                    value={settings.adoPat}
                    onChange={(_e, data) => update("adoPat", data.value)}
                    placeholder="Optional — enables work item access"
                  />
                </Field>
                <Field label="Project Name">
                  <Input
                    value={settings.projectName}
                    onChange={(_e, data) => update("projectName", data.value)}
                    placeholder="MyProject"
                  />
                </Field>
              </div>
            </div>
          </DialogContent>
          <DialogActions>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="secondary">Cancel</Button>
            </DialogTrigger>
            <Button
              appearance="primary"
              onClick={handleSave}
              disabled={!settings.githubToken}
            >
              Save
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}

/**
 * Small icon button that opens the Settings dialog.
 */
export function SettingsButton({
  onSaved,
}: {
  onSaved?: () => void;
}): React.ReactElement {
  const [open, setOpen] = React.useState(false);
  return (
    <>
      <Button
        icon={<SettingsRegular />}
        appearance="subtle"
        size="small"
        title="Settings"
        onClick={() => setOpen(true)}
      />
      <Settings open={open} onOpenChange={setOpen} onSaved={onSaved} />
    </>
  );
}
