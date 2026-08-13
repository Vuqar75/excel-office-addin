import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
const html = fs.readFileSync(new URL("../src/taskpane.html", import.meta.url), "utf8");
new vm.Script(source.replace('import "./taskpane.css";', ""));

const failures = [];
for (const id of ["ai-cell-prompt", "ai-read-context", "ai-generate-preview", "ai-cell-preview", "ai-insert-formula", "ai-load-marker", "ai-process-marker", "ai-process-markers", "ai-result"]) {
  if (!html.includes(`id="${id}"`)) failures.push(`Missing AI control: ${id}`);
}
for (const marker of ["requestAiFormula", "loadAiMarkerFromSelection", "replaceAiMarkers", "processSelectedAiMarker", "processAllAiMarkers", "backupFormulaSheet", "formulaRangeContainsCell", "formulaReferencesCell", "recordAiUsage"]) {
  if (!source.includes(marker)) failures.push(`Missing AI workflow marker: ${marker}`);
}
for (const marker of ["formula-keep-backup", "ai-cell-keep-backup", "discardSuccessfulFormulaBackup", "insertLibraryFormulaWithMandatoryBackup", "normalizeSelectedFormulaDisplay", "isDateNumberFormat", "autofitColumns"]) {
  if (!source.includes(marker)) failures.push(`Missing optional backup marker: ${marker}`);
}
if (!source.includes('/^AI\\s*:\\s*(.+)$/is')) failures.push("AI cell-marker parser is missing");
if (!source.includes('markers.length>20')) failures.push("Batch AI request limit is missing");
if (!source.includes('"ai-process-marker":"заменить выбранный AI-промт')) failures.push("Selected marker safety confirmation is missing");
if (!source.includes('"ai-process-markers":"заменить все AI-промты')) failures.push("Batch marker safety confirmation is missing");
if (/ai:\{[^}]*values/s.test(source)) failures.push("Workbook values must not be included in the AI payload");
if (failures.length) throw new Error(failures.join("\n"));

export const report = { controls: 9, markerPrefix: "AI:", batchLimit: 20, backup: true, journal: true, cellValuesSent: false };
