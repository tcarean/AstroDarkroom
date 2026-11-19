# 0030. A value after `-o` that starts with a dash is a usage error

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

`Options.Parse` took whatever followed `-o` or `--output` as the new file's name. CR-015 of the
review of 2026-11-16 showed the slip it lets through: `copywcs solved.fit stack.fit -o --dry-run`,
the file name forgotten, gave `Output=--dry-run DryRun=False`, a real copy into a file named
`--dry-run` in the working folder, and `-o --force` lost the force. A test,
`OptionsTests.Parse_OutputValueLooksLikeAFlag_ConsumedVerbatimAsFilename`, pinned that behaviour
with the comment "Existing behaviour", and no requirement asked for it, so changing it was the
owner's call (the kit's rule "No test edited to pass").

## Decision

A value after `-o` or `--output` that starts with a dash is R-002's usage error, with the line
the parser already has for a missing value, `--output needs a file name.` (`--output cere un nume
de fisier.`), the usage and exit 2; nothing is written. Every such value, not only the tool's own
flags: a source or a destination that starts with a dash is already an unknown option, so the
three paths follow one rule, and a name that does start with one is written `.\-x.fit`. The owner
chose this on 2026-11-18 and had the pinning test replaced by
`OptionsTests.R002_Parse_OutputFollowedByADash_Throws`.

## Alternatives rejected

- **Refuse only the tool's own flags after `-o`.** `-o -x.fit` would still write `-x.fit`, and
  each new flag would need adding to the list; the one rule is simpler to read and to say.
- **Keep it and reject the entry.** A forgotten name then copies for real where a dry run was
  asked, the opposite of what was typed.
- **A `--` that ends the options.** The rest of CR-026 (a path that starts with a dash), still
  open; `.\` covers the rare name.

## Consequences

The text `--output needs a file name.` is now asserted by a test, which CR-029 noted none did.
Guarded by `OptionsTests.R002_Parse_OutputFollowedByADash_Throws`,
`Parse_OutputValueWithADashInside_IsTheFileName` and
`CopyWcsCliTests.R002_Run_OutputFollowedByAFlag_IsAUsageErrorAndWritesNothing`.
