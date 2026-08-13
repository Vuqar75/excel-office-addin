# Excel Data Assistant 3.1.0

Security and reliability release paired with Desktop Companion 0.8.0.

- Confirmation is bound to the exact selected range.
- Previously unguarded mutating tools now use the safety center.
- Formula insertion rejects external-workbook and external-data functions.
- CSV export neutralizes formula-like cell values.
- Prompt generation and exact paste refuse occupied targets and create backups.
- Settings import is size-limited and schema-validated.
- Backups are associated with the current document session; table restoration removes the created table object.
- Desktop commands use protocol 2, origin and loopback checks, version validation, serialization, and bounded client waits.
- Companion AI requests validate dimensions and generated formulas.
- CSV and split exports avoid overwriting existing files; COM cleanup is strengthened.

