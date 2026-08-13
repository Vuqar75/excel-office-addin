# Excel Data Assistant 3.4.2

Formula result display correction.

- Formula insertion reads the target value and number format after calculation.
- An ordinary numeric result placed in a date/time-formatted empty cell is switched to `General`.
- Date-returning formulas retain their date/time formatting.
- The target column is auto-fitted after successful insertion.
- The completion status reports the target address and applied format.

Validation: ten regression suites and fifteen static release checks.
