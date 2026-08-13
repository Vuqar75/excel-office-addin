# Excel Data Assistant 3.3.0

Unified workbook data-source release.

- Multi-cell selections remain authoritative.
- A single populated cell expands to its current contiguous data region.
- A single empty cell resolves to the active sheet's used range.
- Users can force selection-only, current-region, or whole-used-range mode.
- The resolved address, dimensions, and source reason are shown before the original operation runs.
- Formula/date/paste/dropdown target operations remain explicit and are never auto-expanded.
- Dropdown values and sheet names can be loaded from a workbook range.
- Conditions, filters, AI formula tasks, and IFERROR replacements can be loaded from an active cell.
- Auto-resolved sources keep the existing 100,000-cell safety limit.

Validation: nine regression suites, a dedicated data-source classification test, protocol compatibility tests, JavaScript syntax checks, and fifteen release checks.
