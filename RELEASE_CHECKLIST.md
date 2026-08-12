# Release checklist

1. Update `APP_VERSION`, `package.json`, manifest version and both manifest URLs.
2. Run the 13 static release checks.
3. Run all five regression suites.
4. Confirm 67 unique formula definitions build in three address contexts.
5. Confirm all 13 panels are covered by five navigation groups.
6. Confirm all guarded operations and formula insertion safety markers.
7. Build publication files with inline CSS and without the JavaScript CSS import.
8. Confirm one Office.js script and one versioned taskpane script.
9. Publish JavaScript first, then HTML.
10. Wait for GitHub Pages and verify live version, script query, styles and `Open in Excel` status.
11. Do not request manifest reload for an ordinary web release.
12. Keep the previously accepted version as the rollback baseline until the new version is verified.
