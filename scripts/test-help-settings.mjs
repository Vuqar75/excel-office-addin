import fs from "node:fs";
import vm from "node:vm";

const root = new URL("../", import.meta.url);
const source = fs.readFileSync(new URL("src/taskpane.js", root), "utf8");
new vm.Script(source.replace('import "./taskpane.css";', ""));

const required = [
  "initializeHelpCenter()",
  "assistant-help-center",
  "export-assistant-settings",
  "import-assistant-settings",
  "PORTABLE_SETTING_KEYS",
  "Секретный ключ OpenAI не экспортируется",
  "Ключ OpenAI не изменялся",
  "data.product!==\"Excel Data Assistant\"",
  "downloadText("
];
const missing = required.filter(marker => !source.includes(marker));
if (missing.length) throw new Error(`Missing help/settings markers:\n${missing.join("\n")}`);

const keyStart = source.indexOf("const PORTABLE_SETTING_KEYS=") + "const PORTABLE_SETTING_KEYS=".length;
const keyEnd = source.indexOf(";", keyStart);
const keys = vm.runInNewContext(`const keys=${source.slice(keyStart, keyEnd)};keys`, {}, { timeout: 1000 });
if (keys.length !== 6 || keys.some(key => /key|token|secret/i.test(key))) throw new Error(`Unsafe or incomplete portable keys: ${JSON.stringify(keys)}`);

for (const file of ["README.md", "USER_GUIDE.md", "RELEASE_CHECKLIST.md", "PROJECT_STATE.md"]) {
  const text = fs.readFileSync(new URL(file, root), "utf8");
  if (text.trim().length < 200) throw new Error(`Documentation is missing or too short: ${file}`);
}

export const report = { portableSettings: keys.length, helpSections: 3, documentationFiles: 4 };
