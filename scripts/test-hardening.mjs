import fs from "node:fs";

const js = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
const companion = fs.readFileSync(new URL("../desktop-companion/Program.cs", import.meta.url), "utf8");

const webMarkers = [
  "safetyBypass",
  "Выделение изменилось:",
  "Диапазон результата",
  "Целевой диапазон",
  "Введите непустое условие фильтра",
  "WEBSERVICE|RTD|HYPERLINK",
  "Файл настроек превышает 1 МБ",
  "unsaved:${documentSessionKey}",
  "setTimeout(()=>URL.revokeObjectURL(a.href),1000)",
  "version:APP_VERSION",
  "timeoutMs:900000"
  ,"findDuplicateKeys(a.slice(1),key,cs)"
  ,"function findDuplicateKeys(rows,k,cs=false)"
];
const companionMarkers = [
  "const int ProtocolVersion = 2",
  "SemaphoreSlim commandGate",
  "Headers.Origin",
  "IPAddress.IsLoopback",
  "System.Version.TryParse",
  "FormulaSafety.IsSafe",
  "UniqueFilePath(outputFolder, name, \".csv\")",
  "finally { Release(sheet); }"
];

const missing = [
  ...webMarkers.filter(marker => !js.includes(marker)).map(marker => `web:${marker}`),
  ...companionMarkers.filter(marker => !companion.includes(marker)).map(marker => `companion:${marker}`)
];
if (missing.length) throw new Error(`Hardening markers missing: ${missing.join(", ")}`);

const report = { webGuards: webMarkers.length, companionGuards: companionMarkers.length };
console.log(`PASS: ${report.webGuards} web hardening guards; ${report.companionGuards} companion guards`);
export { report };
