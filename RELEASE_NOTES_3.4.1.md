# Excel Data Assistant 3.4.1

Formula insertion usability update.

- Formula Library and AI Assistant insertion buttons now read `Вставить`.
- A separate unchecked option controls whether the populated safety backup is retained after success.
- A populated temporary backup is still created before every formula insertion.
- When insertion succeeds and retention is not selected, the temporary backup and its restore-ledger entry are removed.
- If insertion or backup cleanup fails, the backup is retained for recovery.

Validation: ten regression suites and fifteen static release checks.
