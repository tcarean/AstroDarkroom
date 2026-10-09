# 0002. The layout stays src/ and tests/; kit.json holds the values; a release publishes framework-dependent

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The kit's scripts, skills and agent are the same bytes in every project and read what differs
from `kit.json` (the kit's ADRs 0017 and 0018). Mindfulness Reminder already has the kit's
layout: `src/MindfulnessReminder.Core/`, `src/MindfulnessReminder.App/`,
`tests/MindfulnessReminder.Tests/` and one solution, `MindfulnessReminder.sln`. It has no test
data (ADR 0005). Its README documents one way to deploy, in "Deployment note": a
framework-dependent `dotnet publish` of the App project in Release into a stable folder, since
the Run entry records the executable's path and the app is installed once, on a machine that
has the .NET 8 desktop runtime. A publish that names a runtime makes the kit's ADR 0034 apply:
`<RuntimeIdentifiers>` and, for a single file, `<EnableSingleFileAnalyzer>` in
`Directory.Build.props`, so the publish does not rewrite the lock files. The owner chose on
2026-10-09, approving the plan: "publish framework-dependent".

## Decision

`kit.json` holds:

- `solution` `MindfulnessReminder.sln`; `testFilter` empty, since there is no validation
  category to keep out of the gate (ADR 0005);
- `codeFolders` `src/`, `codeFiles` none, `testFolders` `tests/`;
- `testData.folder` `tests/testdata/`, `testData.largeEnv` `MINDFULNESS_TESTDATA_LARGE`, the
  name `kit-config.ps1` requires, which nobody sets;
- `mutate.testProject` `tests/MindfulnessReminder.Tests/`;
- one `release.publish` entry: `src/MindfulnessReminder.App/MindfulnessReminder.App.csproj`
  into `artifacts/<version>/` itself, with no extra arguments: a framework-dependent, any-CPU
  publish, the one the README's "Deployment note" documents;
- `audit.alsoRead` `README.md`, the user documentation, which states behaviour and is not code;
- `kit` `../ClaudeCodeStarterKit`.

No runtime is declared in `Directory.Build.props`: the kit's ADR 0034 does not apply.

## Alternatives rejected

- **A self-contained `win-x64` publish.** It would carry the runtime with it, and needs the
  two lines of the kit's ADR 0034 and a sentence in the README's deployment note; the owner
  chose the framework-dependent publish the README already documents.
- **Moving the projects.** The layout is the kit's default already; nothing to move.

## Consequences

- `scripts/release.ps1` publishes the application into `artifacts/X.Y.Z/` and tags
  `mindfulnessreminder-vX.Y.Z`; the lock files carry no runtime section, and the publish
  changes none of them.
- A release that later names a runtime takes the lines of the kit's ADR 0034 with it.
- The `allow` list of `.claude/settings.json` gains two entries of the project's own, for
  `MindfulnessReminder.exe --*` (`--preview`, `--detect`), which take no mutex and write
  nothing.
