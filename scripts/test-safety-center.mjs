import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
new vm.Script(source.replace('import "./taskpane.css";', ""));

const required = [
  "initializeSafetyCenter()",
  "const SAFETY_ACTIONS=",
  "const DESKTOP_ONLY_ACTIONS=",
  "document.addEventListener(\"click\",interceptRiskyAction,true)",
  "event.stopImmediatePropagation()",
  "range.load([\"address\",\"rowCount\",\"columnCount\"])",
  "Нажмите исходную кнопку ещё раз в течение 10 секунд",
  "safetyArmed.set(button.id",
  "decorateCompatibility()",
  "data-compatibility",
  "operation-preview-cancel"
];
const missing = required.filter(marker => !source.includes(marker));
if (missing.length) throw new Error(`Missing safety center markers:\n${missing.join("\n")}`);

const actionsStart = source.indexOf("const SAFETY_ACTIONS=") + "const SAFETY_ACTIONS=".length;
const actionsEnd = source.indexOf(";", actionsStart);
const actions = vm.runInNewContext(`const actions=${source.slice(actionsStart, actionsEnd)};actions`, {}, { timeout: 1000 });
const expected = ["apply-cleaning", "remove-duplicates", "filter-action-run", "formulas-to-values", "smart-fill-down", "smart-fill-right", "apply-precision", "unmerge-fill", "create-dynamic-table"];
for (const id of expected) if (!actions[id]) throw new Error(`Risky action is not guarded: ${id}`);
if (Object.keys(actions).length < 18) throw new Error(`Expected at least 18 guarded actions, got ${Object.keys(actions).length}`);

export const report = { guardedActions: Object.keys(actions).length, requiredMarkers: required.length, confirmationSeconds: 10 };
