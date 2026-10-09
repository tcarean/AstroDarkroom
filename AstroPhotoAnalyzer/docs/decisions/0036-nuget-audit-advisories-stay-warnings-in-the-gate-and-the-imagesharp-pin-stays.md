# 0036. NuGet audit advisories stay warnings in the gate; the ImageSharp pin stays

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

On 2026-10-07 the GitHub Advisory Database published five advisories against
`SixLabors.ImageSharp` up to 4.1.1, all patched in 4.1.2 only: GHSA-j3p4-wp97-rph4 (high, an
out-of-bounds read in `HistogramEqualization` on a float TIFF with infinite samples),
GHSA-jjfr-hcj7-qf5w (high, a buffer overrun in the 1-bit CCITT Group 4 TIFF encoder),
GHSA-j9gm-c75j-xc9q (high, the same in the CCITT Group 3 encoder), GHSA-gwg2-r3hj-4w44
(moderate, a large allocation from a malformed embedded ICC profile's CLUT dimensions) and
GHSA-wmxv-xphr-5c9g (moderate, a BigTIFF whose IFD makes the decoder loop without progress).
NuGet's audit reports them at restore as NU1903 and NU1902, the build replays them, and the
gate's `-warnaserror` (ADR 0003) turns them into errors: the first fresh restore after the
publication, in the worktree of the lift-table task, made `scripts/verify.ps1` red on a
branch that touched no package. The main checkout still built only because its restore of
2026-10-05 predates the advisories. 3.1.11 is pinned by ADR 0009; 4.x was rejected there for
its licence-key MSBuild task, which errors on Release without a key.

## Decision

`Directory.Build.props` lists NU1902 and NU1903 in `MSBuildWarningsNotAsErrors`: the gate keeps
printing every advisory and stays green on them. The pin on 3.1.11 stands. Each advisory is
read against what the tool does with the package and the judgment is recorded in
`docs/design/dependencies.md`; the five above do not apply: the tool never calls
`HistogramEqualization`, never encodes 1-bit or CCITT TIFFs (it writes 8-bit LZW), reads
BigTIFF and ICC profiles only from the owner's own finished images, which are not
attacker-supplied, and the worst outcome of each is a crash or a large allocation of a
local tool, not code execution or data loss. The owner chose this on 2026-10-09 over the
upgrade.

## Alternatives rejected

- **Upgrade to ImageSharp 4.1.2.** The only patched version is the line ADR 0009 rejected: a
  licence key for every Release build, a licence re-read, and the four measured behaviours of
  3.1.11 to re-measure. A task of its own if the owner ever wants it; not a side effect of a
  performance branch.
- **`NuGetAudit` off, or `NuGetAuditLevel` critical.** Both would hide the advisories; the gate
  should show them, so that the next one is read.
- **Suppress the two codes in `NoWarn`.** The same hiding, by another name.
- **Leave the gate red until the upgrade.** No commit could pass it; the rule "Gate" is meant
  to catch the branch's own faults, not an advisory published in the meantime.

## Consequences

- `scripts/verify.ps1` prints the NU1902/NU1903 lines on every fresh restore and passes; a
  reader of the gate's output sees them. A new advisory is a question: read it, record the
  judgment in `docs/design/dependencies.md`, and if it does apply, open the upgrade task.
- The exemption is the project's, in `Directory.Build.props`, not the kit's: the kit's
  `verify.ps1` keeps `-warnaserror` unchanged (the kit's ADRs 0017 and 0028).
- FlatCheck and ImageVisualCompare pin the same package and will meet the same five advisories
  at their next fresh restore; each decides for itself.
