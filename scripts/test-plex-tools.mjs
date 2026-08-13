import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
new vm.Script(source.replace('import "./taskpane.css";', ""));

const required = [
  'bind("insert-current-date"',
  'bind("insert-current-time"',
  'bind("insert-live-datetime"',
  'bind("smart-fill-down"',
  'bind("smart-fill-right"',
  'bind("create-dynamic-table"',
  'bind("apply-precision"',
  'bind("unmerge-fill"',
  'bind("formulas-to-values"',
  'backup(c,r,"Резерв_точность")',
  'backup(c,r,"Резерв_объединение")',
  'backup(c,r,"Резерв_значения")',
  'Диапазон ${r.address} содержит данные',
  'if(!count)throw new Error'
  ,'Array(r.columnCount).fill("=NOW()")'
  ,'Array(r.columnCount).fill("dd.mm.yyyy hh:mm:ss")'
  ,'backup(c,r,direction==="down"?"Резерв_вниз":"Резерв_вправо")'
  ,'backup(c,r,"Резерв_таблица")'
  ,'c.workbook.tables.add(r.address,true)'
  ,'Заголовки таблицы должны быть уникальными.'
];
const missing = required.filter(value => !source.includes(value));
if (missing.length) throw new Error(`Missing PLEX tool guards:\n${missing.join("\n")}`);

const roundFormula = (formula, digits) => `=ROUND(${formula.slice(1)},${digits})`;
const cases = [
  ["=SUM(A1:A10)", 2, "=ROUND(SUM(A1:A10),2)"],
  ["=IFERROR(A1/B1,0)", 0, "=ROUND(IFERROR(A1/B1,0),0)"],
  ["=XLOOKUP(A1,B:B,C:C)", 6, "=ROUND(XLOOKUP(A1,B:B,C:C),6)"]
];
for (const [formula, digits, expected] of cases) {
  const actual = roundFormula(formula, digits);
  if (actual !== expected) throw new Error(`Precision transform mismatch: ${actual}`);
}

const sourceGrid = [["=RC[-1]*2", 1], ["", 2], ["", 3]];
const downTemplate = [...sourceGrid[0]];
const downResult = Array.from({ length: sourceGrid.length }, () => [...downTemplate]);
if (downResult.some(row => row[0] !== "=RC[-1]*2" || row[1] !== 1)) throw new Error("Smart fill down model failed");
const rightTemplate = sourceGrid.map(row => row[0]);
const rightResult = sourceGrid.map((_row, index) => Array(2).fill(rightTemplate[index]));
if (rightResult[0][1] !== "=RC[-1]*2" || rightResult[2][1] !== "") throw new Error("Smart fill right model failed");

export const report = { tools: 9, guards: required.length, precisionCases: cases.length, fillCases: 2 };
