# Mode 7: the synthetic flat from the lens profile (--synthetic, --lenses, --against)

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the decisions of the mode are ADR 0028 to
> ADR 0032, the model definitions, the catalog mechanics and the acceptance figures are in
> `lens-profiles.md`, and this note keeps the mechanics of the report sentences, the lens list
> and the Sintetic tab. **Covers:** in full, `Report/SyntheticReport.cs` (the format and request
> notes, `LensDetails`), `Runs/SyntheticRun.cs` (the mount choice, the shooting data), the
> Sintetic tab (`SyntheticTabViewModel`, `SyntheticView`); by pointer, `LensProfiles/`,
> `Analysis/SyntheticFlatBuilder.cs`, `Report/AgainstReport.cs`, `Runs/AgainstRun.cs`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

### 7. Synthetic flat - `flatcheck ref.fit --synthetic out.fit --lens <text>`, `--lenses`, `--against`

Not a test: it **writes a radial flat from the lens's published vignetting profile** -
the lens's model, not a measurement of the user's copy - and `--against` reads how much
and in what shape a real flat differs from it (or from any other flat). Why it was built
(no illumination available here gives the vignetting depth to better than half a stop) is
ADR 0031's context; `lens-profiles.md` is the evidence file, its section 1 the session that
showed it.

**Two sources, one list.** Where the Adobe and lensfun profiles come from, how they are read
and why nothing is copied or embedded is ADR 0028; the two models, quoted from their sources,
and the model as implemented are `lens-profiles.md` sections 2 to 4; the camera's crop as
the frame's physical size, the SFF as the fallback, `SyntheticReport.FormatMatch` and
`SyntheticRun.PreferCamera` are ADR 0029. The "format" sentence reads "profil calibrat pe
APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format", or "alt format, raza se
recalculeaza prin mm; coltul tau cade la 1.50 x coltul de calibrare: extrapolare MARE"; the
console prints it at "format", the tab shows it when a lens is picked and again under the
written depths. Console and window agree, and the tab's chips are only for a body the
lensfun table lacks: the note under the fields says so and the chosen chip's crop lands in
the box.

**The request note.** The nearest profile without interpolation and the warning for a
request far from every profile are ADR 0029. The sentence (`SyntheticReport.ProfileMismatch`)
names what was asked, what was taken and the lens's range, and that the written depth is the
profile's; the console prints it under "profil folosit", the Sintetic tab shows it under the
fields while typing (`RequestNote`, computed with `FormatNote`) and under "profil folosit"
after the write (`WrittenRequestNote`), and the structure comparison appends it to the row's
note with `IsWarning` when the raw's focal / f-stop is far from the lens's profiles.
`SyntheticFlatResult.LensProfiles` and `ProfileDepth.LensProfiles` carry the lens's whole
profile list so the sentence can state the range.

**The lens and the shot.** The same lens exists per mount in Adobe, so the mount is chosen
by the reference frame's camera maker word (`INSTRUME` / libraw) and an exact name match
beats partial ones; what still ties is listed and the exit code is 2. Focal from `FOCALLEN`
or libraw, f-stop from libraw only (Siril writes no f-number) or `--fstop`; `LoadedFrame`
carries `FocalMm`, `FNumber`, `Camera`, `NikonLensId` for it, filled by both readers
(`FitsReader.LoadHeader` reads the header alone, for the window's prefill on a 96 MB
master). Identifying the lens from the raw's Nikon ID and MakerNote range, why the ID alone
is not enough, and what the window does with several survivors (`OfferCandidates`) are
ADR 0030.

**The file and the list.** Output: float, 1.0 on the axis, the reference frame's cards
minus the structural ones (`BAYERPAT` included, so Siril takes it as a CFA master),
`HISTORY` naming source, lens, profile and model; nothing renormalised. Exit 0 written,
1 refused (cube, or focal / f-stop / crop unknown), 2 the lens could not be chosen.
`--lenses` lists lenses with focals, apertures and distances (`SyntheticReport.LensDetails`:
metres ascending, then "infinit", then "nespecificata" for the collapsed lensfun pair, plus
the profile count) only when the filtered list is at most `SyntheticReport.ListDetailsMax`
(40) long, because the details need the Adobe file parsed. The Sintetic tab shows the same
line under each row, by the same rule: `LensRow.Details` is filled in the background
(`RefreshDetailsAsync`, generation-counted, parallel over the visible rows, `DetailsPending`
for the tests), and on a longer list the rows stay bare and `ListNote` says why
(`SyntheticReport.ListDetailsHidden`). lensfun rows would be free to fill on any list, and
are deliberately held to the same rule so a long list is not half with details and half
without.

**`--against`** is ADR 0032: three structure analyses (A, B and the ratio A / B) printed in
three columns with notes written about the difference, the verdict of the difference as the
worse of shape and depth with the exit code following, the depth row as the only one with a
verdict in the difference column, the `CompareRow` and `DepthRowTemplate` layouts of the
window. The mechanics of the analyzer and the depth arithmetic are `lens-profiles.md`
section 7.

**Measured on the Tokina masters against the Adobe synthetic** (corner, stops, 2026-09-18
with the camera's crop): the close-monitor master +0.19, the sky master -0.39, the
on-monitor master +0.12; Adobe against the lensfun 7D2 entry -0.18 with a PERFECT ratio
(both radial), against the D7100 entry -0.25. These are the acceptance numbers of the mode
(ADR 0032; `lens-profiles.md` section 9 is the procedure; `docs/validation.md` V-036 to
V-040 re-measure them), and they say which real master was wrong (the sky one, by 0.4 stops)
and by how much the panel adds at mid-field (0.1, `lens-profiles.md` section 8).

**What the report says it cannot contain:** the chromatic part of the falloff (on the sky
master R falls ~25 % deeper than B), body-specific shading, dust, any non-radial
structure, the user's own copy; and that the two databases differ by ~0.2 stops on the
same lens. Pinned by `VignettingModelTests` (the piecewise cross-check, the decentred
model, the lensfun normalisation at r = 1), the reader tests on hand-written XML, the
catalog tests, `SyntheticFlatTests` (a synthetic is PERFECT for the structure battery;
against itself it is PERFECT at zero stops), `SyntheticRunTests`, and the golden files
`synthetic_written`, `synthetic_lensfun`, `synthetic_ambiguous`, `lenses`, `against_radial`.

**The window's Sintetic tab** (`SyntheticTabViewModel`, `SyntheticView`): a searchable
list with a source chip and the mount per row, focal / f-stop / crop text boxes prefilled
from the master's header and from the first raw in `flats/` (after the opening chain, in
the background, metadata only; a raw libraw cannot open leaves the f-stop empty and says
so), the sources panel collapsed by default (ADR 0028; it is documentation, not a
precondition, and `SyntheticReport.AdobeWhere` / `LensfunWhere` keep console and window
saying the same), the write button on the Netezire pattern (ADR 0036: waits for idle, asks
before overwriting, never the session's master), and the comparison - `AgainstRun` on the
master and the file just written: the verdict, the three-column rows and the map on a
switchable scale. **The comparison follows the write by itself** (`RunAsync` chains
`CompareAsync(Report)`, which appends to the write's report instead of starting a second
banner) after a real write - not after a refused overwrite, whose old file may be another
lens's, and not after a write error. The first cut needed a second click, and the screen
after "Scrie" - depth table, no comparison - was reported as a bug. "Compara cu masterul"
stays for a file written in an earlier session. The catalog is located once, in the
background, on the first `Attach` (`Func<ProfileSources>` in the `MainViewModel`
constructor for the tests). `SyntheticTabTests` and two smoke renders pin it.
