import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../data-source.js", import.meta.url), "utf8");
new vm.Script(source);

const requiredActions = [
  "analyze-selection", "apply-cleaning", "remove-duplicates", "create-report",
  "fill-blanks", "process-text", "change-references", "wrap-iferror",
  "copy-formulas-exact", "formula-map", "formula-read-context", "unpivot-table",
  "pivot-table", "reverse-range", "filter-action-run", "color-map", "export-csv",
  "split-sheet", "build-prompts", "smart-fill-down", "create-dynamic-table",
  "apply-precision", "unmerge-fill", "formulas-to-values"
];
const explicitTargets = ["paste-formulas-exact", "formula-insert", "insert-current-date", "insert-current-time", "insert-live-datetime", "create-dropdown"];
const parameterSources = ["dropdown-values", "sheet-names", "condition-value", "filter-value", "formula-request", "iferror-value"];
const missing = requiredActions.filter(action => !source.includes(`"${action}"`));
const unsafeTargets = explicitTargets.filter(action => source.match(/AUTO_SOURCE_ACTIONS=new Set\(\[[^\]]*/)?.[0].includes(`"${action}"`));
const missingParameters = parameterSources.filter(id => !source.includes(`"${id}"`));
if (missing.length || unsafeTargets.length || missingParameters.length) {
  throw new Error(`Data-source policy failure: missing=${missing}; unsafe=${unsafeTargets}; parameters=${missingParameters}`);
}
for (const marker of ["getCurrentRegion()", "getUsedRangeOrNullObject(true)", "MAX_CELLS", "window.addEventListener(\"click\""]) {
  if (!source.includes(marker)) throw new Error(`Missing resolver marker: ${marker}`);
}
console.log(`PASS: ${requiredActions.length} source actions; ${explicitTargets.length} protected targets; ${parameterSources.length} workbook parameters`);
export const report = { sourceActions: requiredActions.length, protectedTargets: explicitTargets.length, workbookParameters: parameterSources.length };
