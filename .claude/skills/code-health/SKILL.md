---
name: code-health
description: Measure PadSharp's code health - method length, cyclomatic and cognitive complexity, nesting, parameter counts, type size and copied blocks - against the limits the CodeHealthTests gate enforces, and report ranked refactoring opportunities. Use when asked about code health, code quality, complexity, long methods, hotspots, technical debt, "lean", refactor candidates, or when CodeHealthTests fails.
---

The limits are **enforced**: `tests/PadSharp.Tests/CodeHealth/CodeHealthTests.cs` fails `dotnet test` when any method
or type in `src/` goes over them. This skill measures, reads the hotspots and recommends. It doesn't change code;
refactors are a follow-up the user picks, and then go through `finish-change`.

## 1. Measure

```powershell
$env:PADSHARP_CODE_HEALTH_REPORT = '<scratchpad>\code-health.md'
dotnet test --filter-class "PadSharp.Tests.CodeHealth.CodeHealthTests"
Remove-Item Env:PADSHARP_CODE_HEALTH_REPORT
```

The test writes the report before it asserts, so the report is there even when the gate fails. It covers the
scorecard per band, the largest types, the top methods by cognitive complexity, and every method over a limit,
with exceptions marked. The measuring is syntax-only Roslyn in `CodeHealth/CodeMetrics.cs`; `TheMeasuringCountsWhatItShould`
pins how it counts.

## 2. Judge

| Metric | Good | Limit (enforced) |
|---|---|---|
| Method lines | ≤ 15 | 30 |
| Cyclomatic | ≤ 5 | 10 |
| Cognitive | ≤ 5 | 15 |
| Nesting | ≤ 1 | 2 |
| Parameters | ≤ 3 | 4 |
| Type lines (a partial class counts per file) | ≤ 199 | 500 |
| Copied 10-line blocks | | 3 % of code lines |

Between good and the limit is acceptable. Count per metric how many methods are in each band: that shows
whether a problem is a few hotspots or spread out. Compare with the baselines below.

| When | Methods | Max lines | Max cc | Max cognitive | Max nesting | Largest type | Copies | Over a limit |
|---|---|---|---|---|---|---|---|---|
| Scratchpad rework (Nov 2026) | 179 | 103 (`BuildMenu`) | 13 | 15 (`RestoreSession`) | 3 | 1334 (`MainWindow`) | 0 | not gated |
| MainWindow partials (Nov 2026) | 188 | 103 (`BuildMenu`) | 13 | 12 (`OpenFiles`) | 3 | 962 (`MainWindow.cs`) | 0 | not gated |
| Gate enforced (Nov 2026) | 256 | 30 (`RestoreTab`) | 10 (`UpdateStatus`) | 9 (`OnPreviewKeyDown`) | 2 | 356 (`MainWindow.cs`) | 0 | 0, no exceptions |

## 3. Read before recommending

Open every method in the top six by cognitive complexity and by length; never recommend from numbers alone. For
each, name a concrete change that keeps the behaviour, and the tests that hold it: the method's `<File>Tests`
class. Add a test first when the behaviour isn't pinned yet.

- **Split by seam.** `MainWindow` is a partial class: `MainWindow.cs` holds layout and lifetime, and `Menu`,
  `Files`, `Edit`, `View`, `Find`, `Status` and `Session` hold the rest. Put new code in its seam. A new partial
  file needs its own `<File>Tests` class.
- **Long but flat** code, such as a list of menu items or controls, is low risk. Split it into named pieces
  (`FileMenu()`, `AppearanceCard()`) rather than adding abstractions.
- **Parameter groups** that travel together become a record: `SearchQuery`, `SearchHit`, and `SessionTab` for
  `Document.Restore`.
- **Lean first.** Prefer a refactor that removes code over one that adds layers. AOT rules still hold.

## 4. Exceptions: only with a reason

A breach that is genuinely better left as it is goes into `CodeHealthTests.Exceptions` as `Type.Method` (or the
`file.cs` of a type) with a reason of at least a sentence. `EveryExceptionIsMotivatedAndStillNeeded` fails when a
reason is missing, or when the code has come back within the limits; then remove the entry. Never raise a limit to
make a change pass. When the worst value drops well below a limit, consider lowering the limit.

## 5. Report

1. A short verdict, starting with anything over a limit.
2. The scorecard per band.
3. Ranked refactoring opportunities, each with a clickable `file:line`, its numbers and the change.
4. The scratchpad path of the report. When a refactor lands, add a baseline row above.
