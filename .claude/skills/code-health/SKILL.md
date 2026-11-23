---
name: code-health
description: Measure PadSharp's code health - method length, cyclomatic and cognitive complexity, nesting, parameter counts, type and file size, and copied blocks - and report ranked refactoring opportunities. Use when asked about code health, code quality, complexity, long methods, hotspots, technical debt, "lean", or refactor candidates. Read-only - it changes no code unless the user then asks for a refactor.
---

Measure, read the hotspots, recommend. Don't change code in this skill: refactors are a follow-up the user picks,
and then go through `finish-change`.

## 1. Measure

```powershell
dotnet run --file .claude/skills/code-health/metrics.cs -- . --json <scratchpad>\code-health.json
```

`--file` matters: plain `dotnet run x.cs` in the repo root runs PadSharp itself with the script as an argument.
The script is syntax-only Roslyn (no compilation) over every `.cs` file outside `bin`, `obj` and `.claude`, and
prints the scorecard, the top methods by cognitive complexity and length, the largest types and files, every
method in the warning band, and duplicated 10-line blocks.

## 2. Judge

Bands (good / acceptable; above acceptable is the warning band). There is no gate in the build; these are targets.

| Metric | Good | Acceptable |
|---|---|---|
| Method lines | ≤ 15 | ≤ 30 |
| Cyclomatic | ≤ 5 | ≤ 10 |
| Cognitive | ≤ 5 | ≤ 15 |
| Nesting | ≤ 1 | ≤ 2 |
| Parameters | ≤ 3 | ≤ 4 |
| Type lines | ≤ 199 | ≤ 500 |
| Duplication | | ≤ 3 % of code lines |

Count per metric how many methods are in each band: it shows whether a problem is a few hotspots or spread out.
Compare with the baselines below.

| When | Methods | Max lines | Max cc | Max cognitive | Max nesting | Largest type | Copies |
|---|---|---|---|---|---|---|---|
| Scratchpad rework (Nov 2026) | 179 | 103 (`BuildMenu`) | 13 | 15 (`RestoreSession`) | 3 | 1334 (`MainWindow`) | 0 |
| MainWindow split into partials, session/find/status extractions (Nov 2026) | 188 | 103 (`BuildMenu`) | 13 | 12 (`OpenFiles`) | 3 | 962 (`MainWindow.cs`) | 0 |

A partial class is measured per file, so add up its files when judging its size (`MainWindow` is about 1,400 lines
over four files).

## 3. Read before recommending

Open every method in the top six by cognitive complexity and by length; never recommend from numbers alone. For
each, name a concrete change that keeps the behaviour, and how it will be checked (there is no test project: the
build plus a `--screenshot` run, see CLAUDE.md).

- **`MainWindow` is the known hotspot.** It owns menu, toolbar, status bar, find, session and every command. Split
  along those seams into partial-class files or small collaborators (e.g. session save/restore, find handling,
  status bar) rather than adding new layers. Pure logic belongs next to `TextSearch`/`TextFile`, not in the window.
- **Long but flat.** A method that is one straight list of menu items or controls (`BuildMenu`, constructors that
  build UI) is low risk. Say so and rank it below a short method with deep branching.
- **Parameter groups** that travel together become a small record.
- **Lean first.** This is a scratchpad editor whose aim is to stay small (CLAUDE.md). Prefer a refactor that
  removes code or a feature nobody uses over one that adds abstractions.
- **AOT rules still hold**: no reflection, source-generated JSON, compiled XAML.

## 4. Report

1. A short verdict, and first anything far outside the bands.
2. The scorecard per band.
3. Ranked refactoring opportunities, each with a clickable `file:line`, its numbers and the change.
4. The scratchpad path of the JSON report, to compare with later. When a refactor lands, add a baseline row above.
