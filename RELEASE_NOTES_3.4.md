# Excel Data Assistant 3.4.0

AI Assistant release.

- The AI tab is now the single free-form assistant for formula creation and explanation.
- Formula Library remains the predictable, local template catalog.
- A prompt can be typed in the task pane or stored in a worksheet cell as `AI: request`.
- AI output is previewed and validated before ordinary insertion into an explicitly selected empty cell.
- Explicit `AI:` marker cells can be replaced only after the safety preflight, source-range separation check, circular-reference check, and populated sheet backup.
- Up to 20 marker cells can be processed per batch to limit accidental cost.
- Every AI request updates token/cost statistics and every operation is written to the journal.
- Workbook cell values are not included in the OpenAI request; only the explicit task and approved structural metadata are sent.

Validation: ten regression suites and the complete static and live release checks.
