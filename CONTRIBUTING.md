# Contributing to PadSharp

Thanks for helping. PadSharp aims to be a **small** scratchpad editor: it opens instantly, never loses text, and
stays out of the way. Changes that remove code or make something simpler are as welcome as new features. Before
building a large feature, please open an issue first to agree that it fits.

## Getting set up

You need the [.NET 11 SDK](https://dotnet.microsoft.com/download). Then:

```
dotnet build PadSharp.slnx                         # build the app and tests (this is also the lint)
dotnet test                                        # run all tests
dotnet run --project PadSharp.csproj -- notes.txt  # run the app
```

To try the app without touching your real settings and open tabs, point it at a throwaway folder:

```
PADSHARP_DATA=/tmp/padsharp-dev dotnet run --project PadSharp.csproj
```

A running PadSharp locks `bin/Debug`, so close it before building.

## Rules the build enforces

- **Warnings are errors.** .NET analyzers run at `Recommended` and code style is checked on build
  (`.editorconfig`). Fix the code; don't suppress the rule.
- **Native AOT safe.** The trim and AOT analyzers run on every build: no reflection-based JSON (add types to the
  source-generated `SettingsJson` context), and no runtime XAML loading.
- **Every source file has a test class.** `src/Foo.cs` needs `tests/PadSharp.Tests/FooTests.cs`, and
  `ConventionTests` fails the run when one is missing. New behaviour gets a test; a bug fix gets a test that fails
  without the fix. Tests run the real app on Avalonia's headless platform; see `tests/PadSharp.Tests/TestSupport.cs`
  and the Tests section of [CLAUDE.md](CLAUDE.md).
- **Cross-platform.** No Windows-only libraries: PadSharp must build and run on Windows, Linux and macOS.

[CLAUDE.md](CLAUDE.md) describes the architecture and the exact user-visible behaviour, such as status-bar texts,
shortcuts and messages. Update its Behaviour section when you change any of them.

## Pull requests

1. Branch from `main` and keep the change focused: one fix or feature per PR.
2. Make sure `dotnet build PadSharp.slnx` and `dotnet test` pass. CI runs both on Windows, Linux and macOS.
3. For anything a user would notice, add a line under `[Unreleased]` in [CHANGELOG.md](CHANGELOG.md), written for
   users, not developers. Update [README.md](README.md) when a shortcut or feature changes.
4. Describe what changed and how you checked it. For visible changes, include a screenshot; the app can capture
   itself: `PadSharp file.txt --screenshot out.png`.

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).

## Reporting bugs and ideas

Use the issue templates. For a bug, include your operating system, the PadSharp version or commit, and the steps to
reproduce it. For a security problem, don't open an issue; follow [SECURITY.md](SECURITY.md).

Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
