# Excel Data Assistant 3.2.0 / Companion 0.9.0

Security and reliability release.

- Long dropdown lists are stored on a hidden helper sheet and no longer depend on the regional list separator or Excel's 255-character inline limit.
- Data-validation changes now create a backup first.
- Backup sheets are very hidden and a local ledger retains metadata for the 20 most recent backups.
- Formula validation blocks external workbook links, DDE and additional external-call functions, and warns about costly dynamic arrays.
- The local bridge uses one persistent STA dispatcher for every Excel and dialog operation.
- File dialogs have an explicit hidden owner to reduce the risk of opening behind Excel.
- Local requests have size and rate limits and return no-store/nosniff headers.
- Newly generated bridge certificates use non-exportable private keys.
- Publication files are generated deterministically from `src` by `scripts/build-publication.mjs`.

The release gate includes 13 static checks, seven regression suites and a warning-free .NET build.
