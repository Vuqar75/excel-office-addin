import fs from "node:fs";
import vm from "node:vm";

const source = fs.readFileSync(new URL("../src/taskpane.js", import.meta.url), "utf8");
const html = fs.readFileSync(new URL("../src/taskpane.html", import.meta.url), "utf8");
new vm.Script(source.replace('import "./taskpane.css";', ""));

const groupStart = source.indexOf("const NAV_GROUPS=") + "const NAV_GROUPS=".length;
const groupEnd = source.indexOf(";", groupStart);
if (groupStart < "const NAV_GROUPS=".length || groupEnd <= groupStart) throw new Error("NAV_GROUPS not found");
const groups = vm.runInNewContext(`const groups=${source.slice(groupStart, groupEnd)};groups`, {}, { timeout: 1000 });
const groupedPanels = Object.values(groups).flatMap(group => group.panels);
const tabPanels = [...html.matchAll(/class="tab[^"]*" data-panel="([^"]+)"/g)].map(match => match[1]);
const failures = [];
if (Object.keys(groups).length !== 5) failures.push(`Expected 5 navigation groups, got ${Object.keys(groups).length}`);
if (new Set(groupedPanels).size !== groupedPanels.length) failures.push("A panel occurs in more than one navigation group");
for (const panel of tabPanels) if (!groupedPanels.includes(panel)) failures.push(`Tab panel is not grouped: ${panel}`);
for (const panel of groupedPanels) if (!tabPanels.includes(panel)) failures.push(`Grouped panel has no tab: ${panel}`);
for (const marker of ["tool-global-search", "buildToolSearchIndex", "renderToolSearch", "navigationGroupForPanel", "injectNavigationStyles"]) {
  if (!source.includes(marker)) failures.push(`Missing navigation marker: ${marker}`);
}
if (failures.length) throw new Error(failures.join("\n"));
export const report = { groups: Object.keys(groups).length, tabs: tabPanels.length, coveredPanels: groupedPanels.length };
