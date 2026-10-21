# Document every type and method in the code

## Context

On 2026-10-21 the kit's rule "Documentation comments" (the kit's ADR 0035, merged as 2b2235f)
started asking for an XML documentation comment on every type and method in the code: a
`<summary>`, a `<param>` for each parameter, a `<returns>` for a value. Code written before the
rule gets its comments in a task of its own per project. The owner asked for ImageVisualCompare's:
"Backfill the XML documentation comments in ImageVisualCompare's code folders ... about 30 types
and 143 methods to touch. Plan first. At the end, drop CS1573 from NoWarn in Directory.Build.props
and update its comment (ADR 0003)."

The outcome: every type and method under `src/` documented, no code changed, CS1573 no longer
muted, the gate green. MindfulnessReminder's backfill
(`MindfulnessReminder/docs/plans/2026-10-21-document-every-type-and-method.md`, on its branch) is
the pattern; this plan follows it and says where IVC differs.

## Facts checked (2026-10-21, `main` at 2b2235f)

- **The gaps, measured** with the kit task's scratch counter (`doccount`, a Roslyn syntax walk
  over the folders `kit.json` lists, skipping `bin/`, `obj/` and `*.g.cs`), copied into this
  session's scratchpad and extended to list the incomplete members as well. In `src/` (29 files,
  3,296 lines):
  - types: 45, of which 18 have no summary and 12 lack a `<param>` of a positional record or a
    primary constructor;
  - methods: 147, of which 96 have no summary and 47 lack tags (46 a `<param>`, 33 the
    `<returns>`); 79 return something, one of them a plain `Task` (`MainWindow.OpenPairAsync`);
  - properties, indexers and events: 46, of which 30 have no comment (not required);
  - so **30 types and 143 methods**: 17 and 54 in Core (13 files), 13 and 89 in Wpf (14 files).
    `DividerLayout.cs` and `AssemblyInfo.cs` need nothing.
- **The existing comments:** summaries; `<param>` tags on ten types and three methods; two
  `<exception>` (`FitsHeader.ReadLayout`) and one `<remarks>` (`CompareView.CreateThumbnail`); no
  `<returns>`, no `<inheritdoc/>`. They write `null`, `true` and `false` as plain words, code as
  `<see cref>`, `<paramref>` and `<c>`. The FITS files cite ADR 0029; no comment cites a
  requirement.
- **CS1573 is muted** (`<NoWarn>$(NoWarn);CS1591;CS1573</NoWarn>`). The props comment says it
  stays muted "until the comments written before that rule tag every parameter (ADR 0003)";
  `CLAUDE.md`, "Style", says "(CS1591 and CS1573 muted)"; ADR 0003's Decision records the mute,
  and its Status already reads `accepted 2026-10-02; comments for the "why" only superseded by
  the kit's ADR 0035`. Today six members tag only some parameters: `FitsImageLayout`,
  `FitsRowDecoder`, `ImagePair`, `FitsImageLayout.Describe`, `FitsScale.IsRescaled`,
  `ImagePairLoader.LoadAsync`.
- **The tests have no `<param>` tag at all,** so dropping the mute cannot fail the test project,
  which builds with the same `Directory.Build.props`.
- **The compiler checks the rest already:** CS1570, CS1572, CS1574 and CS0419 fail the build
  (documentation file on, warnings as errors). Whitespace is LF, UTF-8, no trailing space
  (`.editorconfig`, `dotnet format` in the gate).
- **Two doc comments describe what the code no longer does or never did** (Assumption 7).
- **Wrap-up checks the project asks for:** the rule "Run the application" and `docs/skills.md`
  launch the application once on the real pair when anything under `src/ImageVisualCompare.Wpf/`
  or `Directory.Build.props` changes; `/wrap-up` step 2 runs `/validate` when Core or `Imaging/`
  is touched. `TestData` honours `IVC_TESTDATA_LARGE`, so the worktree can read the main
  checkout's copies of the real files instead of copying them.
- **Branches:** no branch or worktree for this task. `claude/Document-every-type-and-method`
  exists already (MindfulnessReminder's, unmerged), and branch names are repository-wide, so this
  one is `claude/Document-every-type-and-method-in-IVC`.

## What each file needs (U: no comment; T: tags missing)

**Core (`src/ImageVisualCompare.Core/`)**: 13 files, 17 types, 54 methods
- `DividerModel.cs`: `ScreenX`, `HandleCentreY`, `Reset` U; `MoveTo`, `NudgeBy`, `Layout`, `HitTest` T
- `FitsHeader.cs`: `FitsImageLayout` T (Width, Height, BZero, BScale); `DisplayRow` T; `Describe` U
  (its one `<param>` stays); `IsFits`, `ReadLayout`, `DataBlockBytes`, `ReadCards`, `Value`,
  `Count` T; `Layout`, `NoImage`, `InvalidHeader`, `TooLarge`, `Text`, `IsTrue`, `Number` U
- `FitsRowDecoder.cs`: `FitsRowDecoder` T (`layout`); `ReadFileRow`, `Convert` T; `IsAboveThreshold` U
- `FitsScale.cs`: `FullScale`, `ToSixteenBits`, `ToByte`, `Level` T; `IsRescaled` U (two `<param>` stay)
- `Geometry.cs`: `SizeD` T (2), `RectD` T (4); `AspectFit` U; `Fit` T
- `ImageInfo.cs`: `ImageInfo` T (7)
- `ImagePairValidator.cs`: `PadPlan` T (3), `PadRequest` T (3); `ImagePairValidator` U; `Inspect`,
  `DescribeMismatch` T
- `MinimapGeometry.cs`: `MinimapLayout` U (its params stay), `MinimapGeometry` U; `Layout`, `ContentPointAt` T
- `PointD.cs`: `PointD` T (2)
- `RenderPlan.cs`: `ImageScalingMode` U, `RenderPlan` U (its params stay), `RenderPlanner` U; `For` T
- `ScrollBarState.cs`: `ScrollBarState` U (its params stay), `ScrollBarStates` U; `Horizontal`, `Vertical` U
- `Viewport.cs`: `ScreenToContent`, `ContentToScreen`, `ZoomBy`, `ZoomOut`, `ClampScale`,
  `ClampOffsets` U; `SetContentSize`, `SetViewportSize`, `ContentPointInsideAt`, `ZoomTo`,
  `ZoomIn`, `PanBy`, `SetOffset`, `CentreOn` T
- `ZoomLadder.cs`: `Next`, `Previous` T

**Wpf without a window (`Imaging/`, `Services/`, `Interop/`, `Version.cs`)**: 9 files, 11 types, 35 methods
- `Imaging/IImageLoader.cs`: `Probe`, `LoadAsync` T
- `Imaging/WicImageLoader.cs`: `Probe`, `LoadAsync`, `Load`, `OpenRead`, `CreateDecoder`, `Describe` U; `Classify` T
- `Imaging/FitsImageLoader.cs`: `Probe`, `LoadAsync`, `Load`, `OpenRead` U; `CreateBitmap`, `Decode` T
- `Imaging/RoutingImageLoader.cs`: `RoutingImageLoader` T (`fits`, `other`); constructor, `Probe`,
  `LoadAsync`, `For` U
- `Imaging/ImagePairLoader.cs`: `ImagePair` T (Before, After), `ImageLoadProgress` U (its params
  stay), `ImagePairLoadResult` T (3), `ImagePairLoader` T (`loader`); `Success`, `Failure`,
  `LoadAsync` U; `IsExpectedLoadFailure` T
- `Imaging/MemoryHousekeeping.cs`: `MemorySnapshot` U (its params stay), `MemoryHousekeeping` U;
  `Describe`, `Megabytes`, `Snapshot` U
- `Services/FileDialogService.cs`: `IFileDialogService`, `ImagePairSelection`, `FileDialogService` U;
  the interface's `PickImagePair` T; the class's `PickImagePair`, `Pick` U
- `Interop/DarkTitleBar.cs`: `Apply` T; `ApplyTo`, `Set`, `DwmSetWindowAttribute` U
- `Version.cs`: `VersionInfo` U; `ReadCopyright`, `Read` U

**Wpf, the visual layer**: 5 files, 2 types, 54 methods
- `App.xaml.cs`: `App` U; `TakeStartupSelection` T (`<returns>`); `OnStartup` U
- `MainWindow.xaml.cs`: `MainWindow` U; constructor, `OnPreviewKeyDown`, `OpenPairAsync`,
  `LoadWithOverlayAsync`, `ShowPair`, `UpdateZoomReadout`, `Describe` U; `ConfirmPaddingAsync`,
  `UpdatePixelReadout` T
- `MessageDialog.xaml.cs`: constructor, `OnPreviewKeyDown`, `Create` U; `Show`, `Confirm` T
- `Controls/CompareView.xaml.cs`: 28 U (constructor, `SetPair`, `Clear`, the four zoom commands,
  the eight overrides, and the 13 private helpers from `SetPixelSize` to `ToRect`);
  `CreateThumbnail` T
- `Controls/MinimapView.xaml.cs`: constructor, `Show`, `Hide`, the four overrides, `EndDrag`,
  `RequestCentre` U

## Assumptions (the kit's rule "Two readings": decided, stated)

1. **Scope** is the 30 types and 143 methods above. Properties, fields, events and enum members
   stay as they are; the generated `obj/*.g.cs` and the XAML have no members of ours.
2. **Tags:** a `<summary>` always; a `<param>` for each parameter, a positional record's and a
   primary constructor's on the type; a `<returns>` for each of the 78 methods with a value,
   seven of them through `<inheritdoc/>` (for a `Task<T>`, the value it completes with), none
   for `OpenPairAsync`'s plain `Task`; no `<typeparam>`, since nothing in `src/` is generic.
3. **`<inheritdoc/>` only where an interface of this project states the whole contract,** once
   its own comment is completed here: `Probe` and `LoadAsync` of `WicImageLoader`,
   `FitsImageLoader` and `RoutingImageLoader`, and `FileDialogService.PickImagePair`; 7 members.
   Own text for the 14 framework overrides (what the override does, not the base class's event),
   the two public synchronous `Load` methods and every constructor.
4. **What a comment says: the contract,** in the voice of the file.
   - **Every coordinate, size, offset and scale names its space:** physical screen pixels
     (`Viewport` and what feeds it), device-independent pixels (the divider, WPF positions, the
     minimap), image pixels (content space); a scale of 1 is one image pixel per physical pixel.
     This is the project's rule "Physical pixels below, device-independent pixels above" carried
     into the tags, where a reader of a signature needs it most.
   - A range where the code enforces one ("not negative", "greater than 0"); what `null` means
     where the type allows it; a non-nullable parameter is not called "not null".
   - **Throws:** an argument guard is said as the parameter's range. A throw for bad input that a
     caller must plan for (a FITS cut short, a file that cannot be read, cancellation) is said:
     as `<exception>` tags in `FitsHeader.cs` and `FitsRowDecoder.cs`, beside `ReadLayout`'s two;
     in prose elsewhere, as `CreateBitmap`'s summary already does. No new `<remarks>`.
   - English; `null`, `true`, `false` as plain words; no new ADR or requirement ID (the FITS
     files' ADR 0029 stays); wrapped at 100 columns, a long tag in the block form `ImagePair`'s
     `Pad` uses; an overloaded name in a `cref` gets its parameter list (CS0419).
   - Example, `Viewport.ZoomTo`: `<param name="anchorScreenX">The anchor, in physical pixels from
     the viewport's left edge.</param>`; `<param name="scale">The new scale, 1 being one image
     pixel per physical screen pixel; clamped to MinScale..MaxScale.</param>`.
5. **Event handlers and overrides tag `sender` and `e`,** briefly: what raises it and what is
   read (`OnDpiChanged`'s two scales are not read: the control asks `VisualTreeHelper`).
6. **Existing comments keep their text,** why-paragraphs included. A sentence that already says
   what a parameter means or what comes back moves into its tag instead of being said twice
   (`ZoomLadder.Next` and `Previous`, `DisplayRow`, `HitTest`'s "True when", `ContentPointInsideAt`'s
   "or null", `PickImagePair`'s "Returns null"); the summary then says what the member does.
7. **Two stale doc comments are corrected, in a commit of their own:**
   - the `ImagePairLoader` summary, "refusing anything the comparison could not honestly
     display", is from the base version (a6e5f10), when a mismatched pair was refused; since
     1.1.0 (ADR 0014) it is padded once `confirmPadding` agrees. The new summary says what the
     loader does: both headers first, the question before any decode, the decodes one after the
     other, an unreadable file or an unanswered mismatch as a message;
   - `ImagePair.DisplayBytes`, "for the memory readout": the readout is `MemorySnapshot` (R-052,
     ADR 0016) and nothing in the application reads `DisplayBytes`; the clause goes.

   **Say no at approval and commit 5 goes.**
8. **The CS1573 mute ends** after the last comment, as asked:
   - `Directory.Build.props`: `<NoWarn>$(NoWarn);CS1591</NoWarn>`; the comment's last sentence
     becomes "CS1573 (a parameter without a param tag while another has one) is not muted: every
     parameter in the code has its tag, so a comment that tags only some fails the build (ADR
     0003, the kit's ADR 0035)."
   - ADR 0003's Status: `accepted 2026-10-02; comments for the "why" only and the CS1573 mute
     superseded by the kit's ADR 0035` (the kit's ADR 0024: only the Status line changes).
   - `CLAUDE.md`, "Style": "(CS1591 muted)".
   - **No new project ADR:** the decision is the kit's ADR 0035 (a `<param>` for each parameter;
     CS1573 among the checks wherever the documentation file is generated), which a project cites
     rather than writes again (the kit's rule "Decisions"). Its plan already named this step.
9. **Nothing else changes:** no requirement, test, README or version. One `Internal` line in
   `CHANGELOG.md`: the comments, the two corrections, and CS1573 now failing a partial comment.
10. **The project's wrap-up checks run although only comments change:** `/validate`, with
    `IVC_TESTDATA_LARGE` pointing at the main checkout's copies; one launch from the worktree's
    build on `de_printat.png` and `de_printat_PRINT_preview_16bit.tif`, with a screenshot
    (a PowerShell `PrintWindow` capture into the scratchpad) looked at.

## Setup

- From `E:\Photografie\Code\ImageVisualCompare`:
  `git worktree add .claude/worktrees/Document-every-type-and-method-in-IVC -b claude/Document-every-type-and-method-in-IVC main`
- `<wt>` = `E:\Photografie\Code\ImageVisualCompare\.claude\worktrees\Document-every-type-and-method-in-IVC\ImageVisualCompare`.
  Edits with the Edit tool on absolute paths there, never in the main checkout. Commits are plain
  `git -C <wt> commit -q -m "..."`; the hook's `== OK in NNs` is expected.
- This plan is saved as `<wt>/docs/plans/2026-10-21-document-every-type-and-method.md`.
- **If the gate goes red for a reason that predates this task, I stop and report it.**

## Commits on `claude/Document-every-type-and-method-in-IVC`

1. The plan.
2. **Core:** 13 files, 17 types, 54 methods.
3. **Wpf without a window:** `Imaging/` (the interface first), `Services/`, `Interop/`,
   `Version.cs`: 9 files, 11 types, 35 methods.
4. **Wpf, the visual layer:** `Controls/`, `MainWindow`, `MessageDialog`, `App`: 5 files, 2 types,
   54 methods.
5. **The two stale doc comments** (Assumption 7).
6. **CS1573 no longer muted** (Assumption 8).
7. **Wrap-up:** the CHANGELOG line, and the plan's "What deviated" with the measured counts.

Each message is one line, under 100 characters, saying why. One session is expected; if it runs
short it stops after a commit, and a session opened in the project folder continues from this
plan and the branch (I give a prompt to paste; a chip would open at the checkout root).

## Verification

- **The gate** green at each commit: the hook's `== OK`, 256 tests as on `main`, and the build's
  CS1570/1572/1574/0419 checks; after commit 6 also CS1573, the compiler's own proof that no
  comment tags only some parameters.
- **The scratch counter** on `<wt>`: in `code`, 0 types and 0 methods undocumented or incomplete
  (now 18 + 12 and 96 + 47); properties still 30 without a comment. Output pasted.
- **Only comments changed:** in `git -C <wt> diff main -- src`, every `+` or `-` line is a `///`
  line; filtered for the rest, the output is empty.
- **`scripts/check-design.ps1 -Since main`:** each note it lists is read against the change; none
  should change, since no mechanics do.
- **`/validate`:** the drift table, none expected. **The launch:** the screenshot shows the pair,
  the status bar's dimensions, formats and version.
- **The whole diff** is read before each commit and summarised at wrap-up.

## Then

- `/wrap-up` in `<wt>`, then I suggest `/code-review main...claude/Document-every-type-and-method-in-IVC`.
- I propose `git merge --no-ff claude/Document-every-type-and-method-in-IVC` in
  `E:\Photografie\Code` and ask who runs it. After the merge: `git worktree remove`, then
  `git branch -d`.
- Memory: `documentation-comments-rule.md` marks ImageVisualCompare done, CS1573 mute dropped.
