#!/usr/bin/env node
/**
 * Bumps the version in azure-devops-extension.json before packaging.
 *
 * Usage:
 *   node scripts/bump-version.js          # auto-detect: pipeline or local
 *   node scripts/bump-version.js --local  # force local (timestamp-based patch)
 *
 * Version format: major.minor.patch
 *   - Minor is incremented each run
 *   - Patch:
 *       Pipeline  → BUILD_BUILDNUMBER env var (set by Azure Pipelines)
 *       Local     → compact timestamp YYYYMMDDHHmm (e.g. 202603211430)
 *
 * The script updates the source file in-place so the webpack copy picks it up.
 */

const fs = require("fs");
const path = require("path");

const MANIFEST = path.resolve(__dirname, "..", "azure-devops-extension.json");

function readManifest() {
  const raw = fs.readFileSync(MANIFEST, "utf-8");
  return JSON.parse(raw);
}

function writeManifest(manifest) {
  fs.writeFileSync(MANIFEST, JSON.stringify(manifest, null, 2) + "\n", "utf-8");
}

function getPatch() {
  const forceLocal = process.argv.includes("--local");
  const buildNumber = process.env.BUILD_BUILDNUMBER;

  if (!forceLocal && buildNumber) {
    // Pipeline: extract digits from build number (e.g. "20260321.5" → "202603215")
    const digits = buildNumber.replace(/\D/g, "");
    const patch = parseInt(digits, 10);
    if (!isNaN(patch) && patch > 0) {
      console.log(`Pipeline detected — using BUILD_BUILDNUMBER: ${buildNumber} → patch ${patch}`);
      return patch;
    }
  }

  // Local: compact timestamp — MMDDHHmm (max 12312359, well within 2147483647 VSIX limit)
  const now = new Date();
  const ts =
    String(now.getMonth() + 1).padStart(2, "0") +
    String(now.getDate()).padStart(2, "0") +
    String(now.getHours()).padStart(2, "0") +
    String(now.getMinutes()).padStart(2, "0");
  const patch = parseInt(ts, 10);
  console.log(`Local build — using timestamp patch: ${patch}`);
  return patch;
}

function main() {
  const manifest = readManifest();
  const current = manifest.version || "1.0.0";
  const [major, minor] = current.split(".").map(Number);
  const newMinor = minor + 1;
  const patch = getPatch();
  const newVersion = `${major}.${newMinor}.${patch}`;

  manifest.version = newVersion;
  writeManifest(manifest);

  console.log(`Version bumped: ${current} → ${newVersion}`);
}

main();
