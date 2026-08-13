import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = relativePath => fs.readFile(path.join(projectRoot, relativePath), "utf8");

const [html, javascript, css, packageText, manifest] = await Promise.all([
  read("src/taskpane.html"),
  read("src/taskpane.js"),
  read("src/taskpane.css"),
  read("package.json"),
  read("manifest.production.xml")
]);

const packageVersion = JSON.parse(packageText).version;
const appVersion = javascript.match(/const APP_VERSION="([^"]+)";/)?.[1];
const manifestVersion = manifest.match(/<Version>(\d+\.\d+\.\d+)\.\d+<\/Version>/)?.[1];
const manifestTaskpaneVersions = [...manifest.matchAll(/taskpane\.html\?v=([0-9.]+)/g)].map(match => match[1]);
const htmlScriptVersions = [...html.matchAll(/taskpane\.js\?v=([0-9.]+)/g)].map(match => match[1]);
const officeScriptCount = (html.match(/appsforoffice\.microsoft\.com\/lib\/1\/hosted\/office\.js/g) || []).length;
const appScriptCount = (html.match(/<script[^>]+src="\.\/taskpane\.js\?v=[^"]+"[^>]*><\/script>/g) || []).length;
const hasExternalCss = /<link[^>]+rel="stylesheet"/.test(html);
const hasInlineCss = /<style>[\s\S]*<\/style>/.test(html);
const browserJavascript = javascript.replace(/^\s*import\s+["']\.\/taskpane\.css["'];\s*/, "");
const compatibilityScriptCount = (html.match(/protocol-compat\.js\?v=3\.2\.3/g) || []).length;

const checks = [];
const check = (condition, message) => checks.push({ condition: Boolean(condition), message });

check(Boolean(appVersion), "APP_VERSION is declared in src/taskpane.js");
check(packageVersion === appVersion, `package.json version matches APP_VERSION (${packageVersion} / ${appVersion})`);
check(manifestVersion === appVersion, `manifest version matches APP_VERSION (${manifestVersion} / ${appVersion})`);
check(manifestTaskpaneVersions.length === 2 && manifestTaskpaneVersions.every(version => version === appVersion), "both manifest task-pane URLs use the current version");
check(htmlScriptVersions.length === 1 && htmlScriptVersions[0] === appVersion, "HTML contains exactly one versioned taskpane.js reference");
check(officeScriptCount === 1, "HTML contains exactly one Office.js reference");
check(appScriptCount === 1, "HTML contains exactly one taskpane.js script element");
check(compatibilityScriptCount === 1, "HTML contains exactly one current protocol compatibility script");
check(!/^\s*import\s+["']\.\/taskpane\.css["'];/m.test(browserJavascript), "published browser JavaScript has no CSS import");
check(hasInlineCss || hasExternalCss, "HTML includes styling");
check(css.includes("#217346") && css.includes("border-radius"), "source CSS contains the primary visual rules");
check(html.includes('id="status"') && html.includes("Подключение к Excel"), "HTML contains the Excel initialization status element");
check(javascript.includes("Office.onReady"), "JavaScript registers Office.onReady");
check(javascript.includes("formulaRangeContainsCell") && javascript.includes("confirmDestructiveButton"), "critical safety guards are present");

for (const item of checks) {
  console.log(`${item.condition ? "PASS" : "FAIL"}  ${item.message}`);
}

const failures = checks.filter(item => !item.condition);
if (failures.length) {
  throw new Error(`Release validation failed: ${failures.length} check(s).`);
}

console.log(`PASS  release ${appVersion} passed ${checks.length} static checks`);
