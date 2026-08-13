import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
new vm.Script(source.replace('import "./taskpane.css";', ""));

const required = [
  "eda-formula-favorites",
  "eda-formula-recent",
  "formula-library-view",
  "formula-favorite-toggle",
  "recordFormulaRecent",
  "validateFormulaCandidate",
  "validateFormulaBeforeInsert",
  "document.addEventListener(\"click\",validateFormulaBeforeInsert,true)",
  "Нарушен баланс круглых скобок",
  "ссылку на внешнюю книгу",
  "изменчивую функцию"
];
const missing = required.filter(marker => !source.includes(marker));
if (missing.length) throw new Error(`Missing formula experience markers:\n${missing.join("\n")}`);

const validatorStart = source.indexOf("function validateFormulaCandidate(");
const validatorEnd = source.indexOf("\nfunction renderFormulaValidation", validatorStart);
const sandbox = {};
vm.runInNewContext(`${source.slice(validatorStart, validatorEnd)};this.validateFormulaCandidate=validateFormulaCandidate;`, sandbox, { timeout: 1000 });
const cases = [
  ["=SUM(A1:A10)", true, 0],
  ["=SUM(A1:A10", false, 0],
  ["=IF(A1=\"x\",1,0)", true, 0],
  ["=NOW()", true, 1],
  ["='[Other.xlsx]Sheet1'!A1", true, 1],
  ["SUM(A1:A10)", false, 0]
];
for (const [formula, valid, warnings] of cases) {
  const result = sandbox.validateFormulaCandidate(formula);
  if (result.valid !== valid || result.warnings.length !== warnings) throw new Error(`Validation mismatch for ${formula}: ${JSON.stringify(result)}`);
}

export const report = { features: 3, validationCases: cases.length, requiredMarkers: required.length };
