import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
const start = source.indexOf("const FORMULA_LIBRARY=") + "const FORMULA_LIBRARY=".length;
const end = source.indexOf("];", start) + 1;
if (start < "const FORMULA_LIBRARY=".length || end <= start) throw new Error("FORMULA_LIBRARY not found");

const library = vm.runInNewContext(`const library=${source.slice(start, end)};library`, {}, { timeout: 1000 });
const contexts = [
  { range: "A1:A10", first: "A1", last: "A10", firstColumn: "A1:A10", lastColumn: "A1:A10", headerRow: "A1:A1", rows: 10, columns: 1 },
  { range: "C5:F24", first: "C5", last: "F24", firstColumn: "C5:C24", lastColumn: "F5:F24", headerRow: "C5:F5", rows: 20, columns: 4 },
  { range: "XFD1:XFD3", first: "XFD1", last: "XFD3", firstColumn: "XFD1:XFD3", lastColumn: "XFD1:XFD3", headerRow: "XFD1:XFD1", rows: 3, columns: 1 }
];

const failures = [];
const ids = library.map(item => item.id);
if (library.length !== 67) failures.push(`Expected 67 formulas, got ${library.length}`);
if (new Set(ids).size !== ids.length) failures.push("Duplicate formula IDs found");

for (const item of library) {
  if (!item.id || !item.name || !item.category || !item.description || !item.keywords || typeof item.build !== "function") {
    failures.push(`Incomplete definition: ${item.id || item.name || "unknown"}`);
    continue;
  }
  for (const context of contexts) {
    let formula;
    try { formula = item.build(context); }
    catch (error) { failures.push(`${item.id} failed to build: ${error.message}`); continue; }
    if (typeof formula !== "string" || !formula.startsWith("=")) failures.push(`${item.id} returned invalid formula`);
    if (/undefined|null/.test(formula)) failures.push(`${item.id} contains an unresolved value: ${formula}`);
    let depth = 0;
    for (const character of formula) {
      if (character === "(") depth++;
      if (character === ")") depth--;
      if (depth < 0) break;
    }
    if (depth !== 0) failures.push(`${item.id} has unbalanced parentheses: ${formula}`);
  }
}

const score = (item, request) => {
  const text = request.toLocaleLowerCase();
  return item.keywords.split(/\s+/).filter(word => word.length >= 3)
    .reduce((total, word) => total + (text.includes(word) ? 2 : 0), 0)
    + (text.includes(item.name.toLocaleLowerCase()) ? 4 : 0);
};
const expectedMatches = new Map([
  ["сумма по нескольким условиям", "sumifs"],
  ["найти последнее совпадение", "lookup_last"],
  ["рабочие дни между датами", "networkdays"],
  ["убрать переносы строк", "remove_linebreaks"],
  ["список дубликатов", "duplicates_only"],
  ["площадь круга", "circle_area"],
  ["собрать таблицу в один столбец", "tocol"]
]);
for (const [request, expected] of expectedMatches) {
  const actual = [...library].sort((a, b) => score(b, request) - score(a, request))[0]?.id;
  if (actual !== expected) failures.push(`Search mismatch for "${request}": expected ${expected}, got ${actual}`);
}

for (const guard of [
  "Целевая ячейка ${target.address} не пустая",
  "formulaRangeContainsCell(formulaContext.address,target.address)",
  "backupFormulaSheet(c,target,\"Резерв_формула\")",
  "bestLibraryFormula(task,context)"
]) if (!source.includes(guard)) failures.push(`Missing safety/integration guard: ${guard}`);

if (failures.length) {
  throw new Error(`FAIL: ${failures.length} problem(s)\n${failures.map(failure => `- ${failure}`).join("\n")}`);
} else {
  const categories = library.reduce((result, item) => ({ ...result, [item.category]: (result[item.category] || 0) + 1 }), {});
  console.log(`PASS: ${library.length} formulas; ${contexts.length} address contexts; ${expectedMatches.size} search scenarios`);
  console.log(JSON.stringify(categories));
}

export const report = { formulas: library.length, contexts: contexts.length, searches: expectedMatches.size, failures };
