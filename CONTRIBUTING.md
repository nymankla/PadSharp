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
- **Code health is gated.** Methods stay within 30 lines, cyclomatic complexity 10, cognitive complexity 15,
  nesting 2 and 4 parameters, and types within 500 lines per file. `CodeHealthTests` fails the run otherwise.
  Split the code; an exception needs a written reason in `CodeHealthTests.Exceptions`.

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

## Releasing

1. Set `<Version>` in `PadSharp.csproj` to the new number, for example `0.2.0`.
2. In `CHANGELOG.md`, rename `## [Unreleased]` to `## [0.2.0] - <yyyy-mm-dd>` and start a new empty `[Unreleased]`.
3. Commit, then tag and push: `git tag v0.2.0 && git push origin v0.2.0`.

The **Release** workflow checks that the tag, `<Version>` and the changelog agree. It tests, packages Windows, Linux
and macOS, and publishes a GitHub release with that changelog section as its notes.

Signing is optional. It runs only when these repository secrets exist:

| Secret | For |
|---|---|
| `WINDOWS_CERT_PFX` (base64), `WINDOWS_CERT_PASSWORD` | Authenticode signing of `PadSharp.exe` |
| `MACOS_CERT_P12` (base64), `MACOS_CERT_PASSWORD`, `MACOS_SIGN_IDENTITY` | Developer ID signing of `PadSharp.app` |
| `APPLE_ID`, `APPLE_TEAM_ID`, `APPLE_APP_PASSWORD` | Notarizing the macOS app |

Without them, Windows builds are unsigned and the macOS app is ad-hoc signed; the README explains how users open
them anyway.

## Reporting bugs and ideas

Use the issue templates. For a bug, include your operating system, the PadSharp version or commit, and the steps to
reproduce it. For a security problem, don't open an issue; follow [SECURITY.md](SECURITY.md).

Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
