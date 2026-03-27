/**
 * Centralized environment and runtime configuration.
 *
 * Values are injected at build time via webpack DefinePlugin
 * and can be overridden via CI/CD pipeline variable replacement.
 *
 * Usage: import { config } from '../utils/config';
 */

export interface AppConfig {
  /** Backend API base URL (e.g. https://devopscopilot-func.azurewebsites.net/api) */
  backendUrl: string;
  /** Maximum file upload size in bytes */
  maxUploadSizeBytes: number;
  /** Supported readable file extensions for AI context extraction */
  readableExtensions: string[];
  /** Blocked file extensions for security */
  blockedExtensions: string[];
}

/**
 * Application configuration singleton. Values come from:
 * 1. Webpack DefinePlugin at build time (BACKEND_URL)
 * 2. Environment variables via CI/CD pipeline replacement
 * 3. Defaults for local development
 */
export const config: AppConfig = {
  backendUrl: (process.env.BACKEND_URL ?? "http://localhost:7071/api").replace(
    /\/$/,
    "",
  ),
  maxUploadSizeBytes: 10 * 1024 * 1024, // 10 MB
  readableExtensions: [
    ".txt",
    ".md",
    ".csv",
    ".json",
    ".xml",
    ".yaml",
    ".yml",
    ".cs",
    ".ts",
    ".tsx",
    ".js",
    ".jsx",
    ".py",
    ".java",
    ".html",
    ".css",
    ".scss",
    ".sql",
    ".sh",
    ".ps1",
    ".log",
    ".config",
    ".env",
    ".ini",
    ".toml",
    ".feature",
    ".gherkin",
  ],
  blockedExtensions: [
    ".exe",
    ".dll",
    ".bat",
    ".cmd",
    ".com",
    ".msi",
    ".scr",
    ".vbs",
    ".wsf",
    ".wsh",
  ],
};

/**
 * Check if a file extension is readable (can be sent as AI context).
 */
export function isReadableFile(fileName: string): boolean {
  const ext = fileName.includes(".")
    ? "." + fileName.split(".").pop()?.toLowerCase()
    : "";
  return config.readableExtensions.includes(ext);
}

/**
 * Check if a file extension is blocked for security.
 */
export function isBlockedFile(fileName: string): boolean {
  const ext = fileName.includes(".")
    ? "." + fileName.split(".").pop()?.toLowerCase()
    : "";
  return config.blockedExtensions.includes(ext);
}

/**
 * Validate a file for upload. Returns an error message or null if valid.
 */
export function validateFileForUpload(file: File): string | null {
  if (file.size === 0) return "File is empty.";
  if (file.size > config.maxUploadSizeBytes) {
    const maxMB = config.maxUploadSizeBytes / 1024 / 1024;
    return `File size exceeds maximum (${maxMB} MB).`;
  }
  if (isBlockedFile(file.name)) {
    return `File type is not allowed for security reasons.`;
  }
  return null;
}
