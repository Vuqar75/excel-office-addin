# Excel Data Assistant project state

Last verified stable state: web add-in `3.1.0`, desktop companion `0.8.0`.

Current validated release candidate: web add-in `3.2.0`, desktop companion `0.9.0`. It adds hidden range-backed dropdown lists, hidden backup metadata history, stricter formula validation, a persistent STA bridge dispatcher, owned file dialogs, request limits and non-exportable keys for newly created bridge certificates. Publication files must be rebuilt with `scripts/build-publication.mjs` before release.

Current validated release candidate: web add-in `3.3.0`, desktop companion `0.9.0`. Version 3.3 adds a unified data-source policy. Data-processing actions resolve multi-cell selection, current region, or used range; target-writing actions remain explicit. Workbook cells and ranges can supply six formerly manual parameters. Nine regression suites and fifteen release checks pass locally.

Current validated release candidate: web add-in `3.4.0`, desktop companion `0.9.0`. Version 3.4 turns the AI tab into the free-form formula assistant, keeps the formula library deterministic, and adds safe `AI:` worksheet markers with preview, source/target checks, populated backups, journaling, usage accounting, and a 20-request batch cap. Ten regression suites cover the release.

The security and reliability hardening is documented in `RELEASE_NOTES_3.1.md`. Static checks, seven regression suites, the companion build, local bridge protocol checks, GitHub Pages deployment, browser execution, console and computed styles passed on 2026-08-13.

## Stable 3.0.0

- Published and live-verified on GitHub Pages with one Office.js script, one `taskpane.js?v=3.0.0` script and 53 source CSS rules.
- Five navigation groups cover all 13 panels; global tool search is enabled.
- 19 mutating actions use the unified 10-second preflight confirmation.
- Formula library contains 67 unique templates with favorites, recent items and local validation.
- Six regression suites and 13 release checks pass.
- Built-in help, settings export/import, user guide and release checklist are present.

## Project locations

- Workspace: `C:\Users\vuqar.tagiyev\OneDrive - socar.az\Documents\ChatGPT\Excel Office Add-in`
- Web source: `src/taskpane.html`, `src/taskpane.js`, `src/taskpane.css`
- Production manifest: `manifest.production.xml`
- Companion source: `desktop-companion/Program.cs`
- Installed companion: `desktop-companion/dist/ExcelDataAssistant.Companion.exe`
- Repository: `https://github.com/Vuqar75/excel-office-addin`
- GitHub Pages: `https://vuqar75.github.io/excel-office-addin/taskpane.html`

## Architecture

The product is an Office.js task-pane add-in that works in Excel Online and desktop Excel. Windows-only operations use a local .NET companion over `https://127.0.0.1:47831`.

The companion uses:

- a CurrentUser local certificate;
- restricted CORS for the GitHub Pages origin;
- a per-process session token;
- Windows DPAPI CurrentUser storage for the OpenAI key;
- OpenAI Responses API with `gpt-5-nano`;
- no transmission of workbook cell values for formula generation.

## Implemented web features

- Selection analysis and metrics.
- Data cleaning with backups.
- Duplicate detection, highlighting, and removal.
- Table comparison.
- Automated reports and charts.
- Cell tools, conditional selection, text processing, dropdowns, and test data.
- Formula-reference conversion, IFERROR wrapping, exact formula copy/paste, and formula map.
- Formula library with 20 local templates.
- Explicit header checkbox and selection context.
- Local formula recommendation and explanation.
- Secure AI formula generation, Russian explanations, and preview-before-insert.
- Safe formula insertion with empty-target, outside-source, and circular-reference checks.
- Sheet, transformation, data, prompt, and history tools.
- AI token/cost tracking and JSON export.
- Recoverable journal clearing.

## Implemented desktop companion features

- Combine workbooks from a folder.
- Mass export worksheets.
- Local Power Query scenario.
- Convert XLS to XLSX.
- Combine CSV files.
- Split a workbook into files by sheet.
- Export sheets to PDF.
- Inspect external links and connections.
- Create a workbook backup.
- Correct filename encoding issues.
- Configure OpenAI key securely.
- Generate formulas through OpenAI.
- Restart the bridge safely.

## Important bugs already encountered

### Office.js `rowCount`

Reading `rowCount` before `load()` and `context.sync()` caused an Office.js property error. Always load proxy properties before reading them.

### Empty formula backup

Backing up only the selected target cell produced an empty backup when the target was empty. Formula insertion now backs up the used range of the source sheet.

### Unsafe formula target

Older code could overwrite an occupied cell or insert inside the source range. Stable code requires a read context, an empty target outside the source, and rejects direct circular references.

### AI response parsing

The model could consume the response allowance with reasoning and return incomplete output. The companion now uses `gpt-5-nano`, minimal reasoning, a larger output allowance, and structured JSON extraction.

### Excess AI warnings

The prompt now requires an empty warning when the formula already matches the explicit range/header context.

### Excel WebView and `confirm()`

Native browser confirmation did not open in Excel WebView. Destructive actions use an in-panel two-click confirmation instead.

### Publishing failures in versions 2.9.0–2.9.2

- `2.9.0`: published HTML omitted CSS.
- `2.9.1`: CSS link existed, but an initial GitHub Pages deploy failed due to a certificate error; Excel also retained a failed external-CSS response.
- `2.9.2`: CSS was embedded, but the published HTML omitted `taskpane.js`, so styling worked while Excel initialization did not.
- `2.9.3`: verified stable recovery with inline CSS and one explicit `taskpane.js` reference.

These failures established the mandatory release checklist in `AGENTS.md`.

## Verified stable 2.10.2 publication properties

- Version label: `EXCEL DATA ASSISTANT 2.10.2`.
- One `taskpane.js?v=2.10.2` script reference.
- Inline styles present.
- 53 CSS rules applied.
- Primary button computed background: `rgb(33, 115, 70)`.
- Primary button corner radius: `7px`.
- JavaScript executes and changes the non-Excel status to `Open in Excel`.
- No browser console errors during verification.
- User confirmed that styling and Excel connection work.

## Current user constraints and preferences

- No administrator rights.
- Cannot load a manifest through desktop Excel.
- Existing installation updates through hosted web assets and a full Excel restart.
- Do not ask the user to reload the manifest for ordinary web updates.
- The user expects functional names to match the actual UI exactly.
- Do not announce a release until the live deployment has been checked comprehensively.

## Next-step policy

Treat `3.0.0` and `0.7.5` as the rollback baseline. Before new feature work:

1. Read this file and `AGENTS.md`.
2. Define one bounded release scope.
3. Make and test all related changes together.
4. Run `scripts/validate-release.mjs`; any failure blocks publication.
5. Keep a copy of the known-good published HTML/JS behavior.
6. Perform the full release checklist.
7. Ask the user for one consolidated acceptance test.
