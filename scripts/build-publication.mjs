import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sourceRoot = path.join(projectRoot, "src");
const [html, css, javascript] = await Promise.all([
  fs.readFile(path.join(sourceRoot, "taskpane.html"), "utf8"),
  fs.readFile(path.join(sourceRoot, "taskpane.css"), "utf8"),
  fs.readFile(path.join(sourceRoot, "taskpane.js"), "utf8")
]);

const browserJavascript = javascript.replace(/^\s*import\s+["']\.\/taskpane\.css["'];\s*/, "");
if (!/<style>[\s\S]*?<\/style>/.test(html)) throw new Error("Inline style block is missing.");
const publicationHtml = html.replace(/<style>[\s\S]*?<\/style>/, `<style>${css}</style>`);
if (/import\s+["']\.\/taskpane\.css/.test(browserJavascript)) throw new Error("CSS import remains in browser JavaScript.");

await Promise.all([
  fs.writeFile(path.join(projectRoot, "taskpane.html"), publicationHtml, "utf8"),
  fs.writeFile(path.join(projectRoot, "taskpane.js"), browserJavascript, "utf8")
]);
console.log("PASS: publication files rebuilt from src");
