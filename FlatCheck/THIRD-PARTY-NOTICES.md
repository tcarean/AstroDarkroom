# Third-party components

FlatCheck is distributed under the **GNU General Public License v3 or later** (see
[LICENSE](LICENSE)). This file lists the third-party code it links, each component's licence and
why that licence is compatible with GPLv3.

It is not a formality: two of the entries below involve a **choice**, not a mere observation.
LibRaw is offered under two licences of which only one is compatible with the GPL, and the
ImageSharp licence is conditional, so it has to be shown that the condition is met. A package
added without an entry here is a package whose compatibility nobody has checked.

What is written by hand and is **not** third-party code: the FITS reader, the PNG encoder, every
computation and every threshold.

---

## Sdcb.LibRaw

**Role.** Reads the undebayered Bayer mosaic, the exact white and black levels, the order of the
colours in the CFA and the bytes of the preview from a camera raw file. Without it, the exposure
mode could not run directly on a `.NEF`.

**Licence.** MIT (the C# wrapper).

**Compatibility.** MIT is permissive and compatible with GPLv3.

- Project: <https://github.com/sdcb/Sdcb.LibRaw>

## LibRaw (the native libraries of `Sdcb.LibRaw.runtime.*`)

**Role.** The actual decoding of the raw formats. It comes as native DLLs (`raw_r`, `jpeg8`,
`lcms2`, `zlib1`), shipped beside the executable.

**Licence.** Dual, at the choice of whoever uses it: **LGPL-2.1-only OR CDDL-1.0**.

**Compatibility: here a decision is taken.** FlatCheck uses LibRaw under the **LGPL-2.1**.

CDDL-1.0 is **not** compatible with the GPL, so choosing the other option would break the tool's
licence. LGPL-2.1 is compatible: its section 3 explicitly allows applying the terms of the
ordinary GPL, "version 2 of the ordinary GNU General Public License or any later version", to a
copy, which brings the code into a GPLv3 work.

If anyone ever changes this option, FlatCheck's licence breaks silently. That is why it is written
here, and not worked out when needed.

- Project: <https://www.libraw.org/>

## SixLabors.ImageSharp

**Role.** Decodes the JPEG preview the camera embeds in the raw file, the very image from which
the camera computed its on-screen histogram. It is used for display only, in the Exposure tab: it
enters no verdict and produces no figure that a decision about the flat rests on.

**Licence.** Six Labors Split License, version 1.0 (June 2022).

**Compatibility.** It is not a single restrictive licence but a **conditional dual** one. The text
grants the Apache 2.0 terms when:

> "You are consuming the Work in for use in software licensed under an Open Source or
> Source Available license."

FlatCheck is under GPLv3, so it is software under an open source licence and meets this
condition. The commercial licence is required only in the other cases, essentially proprietary
use by an entity with an annual revenue of at least USD 1M.

And Apache 2.0 goes into GPLv3 in one direction, which the Apache Software Foundation states
explicitly:

> "Apache 2 software can therefore be included in GPLv3 projects, because the GPLv3 license
> accepts our software into GPLv3 works."

FlatCheck's licence needs no change.

**Two things to keep.**

1. **The version is pinned**, in `Directory.Packages.props`. The licence text is "Version 1.0,
   June 2022" and does not say which version of the licence governs which release. The grant
   comes with the version taken, so the pin is legal hygiene, not only reproducibility. The
   version pinned is **3.1.11**, not 4.x: since 4.0.0 the package brings an MSBuild task that
   validates a licence key at compile time, a warning on Debug and an **error on Release** without
   `sixlabors.lic` or `$(SixLaborsLicenseKey)`. Six Labors grants the key free to open source
   projects, but on request, and anyone who clones the repository would need their own to
   publish. 3.1.x has exactly the same licence text and no task. Moving to 4.x means obtaining the
   key and, in the code, setting `ColorProfileHandling.Preserve` explicitly when decoding.
2. **The condition holds downstream.** GPLv3 requires every derivative to stay GPLv3, which is an
   open source licence too, so anyone who takes FlatCheck meets the same condition in turn.

**The alternative evaluated.** `StbImageSharp` (Unlicense or MIT, no transitive dependencies)
gives, measured on real previews against an independent decoder, identical medians on every
channel, and would have carried no licence note. ImageSharp was preferred for its robustness on
the awkward cases a camera preview can present (progressive JPEG, CMYK/YCCK, unusual subsampling)
and for decoding at a reduced scale. If its licence ever moves, StbImageSharp is the direct
replacement, and the seam is one file wide (`PreviewReader.cs`).

Copyright (c) Six Labors.

- Project: <https://github.com/SixLabors/ImageSharp>
- Licence: <https://github.com/SixLabors/ImageSharp/blob/main/LICENSE>

## NLog

**Role.** Writes the diagnostic log, the text files in the `log` folder beside each executable
that a bug report carries (ADR 0070). It decides nothing and computes nothing: the core and the
two applications hand it finished lines, and without its configuration every call is a no-op.

**Licence.** BSD-3-Clause, the version taken being **6.2.2**.

**Compatibility.** BSD-3-Clause is permissive and compatible with GPLv3; the Free Software
Foundation lists the modified (3-clause) BSD licence as GPL-compatible. Of its three conditions,
the source one does not arise (no NLog source is copied here); the binary one asks the copyright
notice, the conditions and the disclaimer to go with the distribution, and the window's About
shows the notice, the licence's name and its link, as it does for the MIT wrapper (ADR 0040),
while this section reproduces the whole text, word for word from `LICENSE.txt` at the commit
6.2.2 was built from (`d9ebf82`); the third forbids using the authors' names to endorse
FlatCheck, which nothing here does.

> Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
>
> All rights reserved.
>
> Redistribution and use in source and binary forms, with or without
> modification, are permitted provided that the following conditions
> are met:
>
> * Redistributions of source code must retain the above copyright notice,
>   this list of conditions and the following disclaimer.
>
> * Redistributions in binary form must reproduce the above copyright notice,
>   this list of conditions and the following disclaimer in the documentation
>   and/or other materials provided with the distribution.
>
> * Neither the name of Jaroslaw Kowalski nor the names of its
>   contributors may be used to endorse or promote products derived from this
>   software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
> AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
> IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
> ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
> LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
> CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
> SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
> INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
> CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
> ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
> THE POSSIBILITY OF SUCH DAMAGE.

The package's own metadata carries a second notice, `Copyright (c) 2004-2026 NLog Project -
https://nlog-project.org/`.

**The alternatives evaluated.** `Microsoft.Extensions.Logging` needs a second package for a file
sink; Serilog does the same work, and the owner asked for NLog by name. The package
`NLog.Targets.AtomicFile`, which NLog 6 offers for several processes writing one file, is not
taken: the plain file target with `KeepFileOpen = false` covers the two windows a user may open.

- Project: <https://nlog-project.org/>
- Licence: <https://github.com/NLog/NLog/blob/master/LICENSE.txt>

---

## lensfun (database)

**Role.** The lenses' vignetting profiles, for the synthetic flat (`--synthetic`, the Synthetic
tab) and for the structure mode's comparison with the lens profile: the coefficients of the `pa`
model (Pablo D'Angelo / Hugin) per focal length, aperture and distance, plus the cameras' crop
factors. The XML files of `data/db` are copied **unchanged** into the `lensfun/` folder at the
project's root and shipped beside the executable; `lensfun/LENSFUN-VERSION.txt` says which commit
the copy is from and when, and `lensfun/update-lensfun.py` refreshes it. The lensfun library
(LGPL-3) is not used, only the data; the reading and the model are FlatCheck's own
(`FlatCheck.Core/LensProfiles/`).

**Licence.** Creative Commons Attribution-Share Alike 3.0 (CC BY-SA 3.0), which the lensfun
project's README states for "the lens database (data/ subdirectory)".

**Compatibility.** It is data, not code: it is not linked into the program and does not become
part of the GPLv3 work, but is aggregated beside it, unchanged. CC BY-SA asks for two things, and
both are met: **attribution** (the component appears in the About window, here and in the README,
and the folder carries `LICENSE.txt`) and **share-alike for the data**, which holds by itself,
since the data are redistributed under the same licence, unmodified. (CC BY-SA 3.0 is not
declared compatible with GPLv3 for *adaptations*; FlatCheck makes none.)

**Why it ships, instead of being read from the machine like the Adobe profiles.** There is no
standard installation of the lensfun database on Windows; darktable and RawTherapee each keep
their own copy. A copy beside the executable is the one that works with nothing else installed,
and it is updated by replacing the files in the folder.

Copyright (C) the lensfun contributors.

- Project: <https://github.com/lensfun/lensfun>
- Licence of the data: <https://creativecommons.org/licenses/by-sa/3.0/>

---

## The Adobe profiles (.lcp): not a shipped component

The synthetic flat and the structure mode's comparison also read Adobe's lens profiles, when
Lightroom, Camera Raw or the DNG Converter installed them on the machine
(`C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`, `%APPDATA%\Adobe\CameraRaw\LensProfiles` and
`C:\Program Files\Adobe\<application>\Resources\LensProfiles\1.0`, Lightroom's own copy). **They
are not copied, not embedded and not redistributed**: they are Adobe's, they are read where Adobe
put them, and without them the Adobe half of the list is empty. That is why they are not in the
About window's list (nothing of theirs is in the program), but they are named here, so that the
next reader does not add them.

---

## Test components

Used only to build and run the tests, so they are not distributed with the tool.

| package | licence |
|---|---|
| xunit | Apache-2.0 |
| xunit.runner.visualstudio | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| coverlet.collector | MIT |

---

## How an entry is added

See the kit's rule "Packages" (`.claude/rules/kit/process.md`), the rule "Package notices" in
[CLAUDE.md](CLAUDE.md) and the note [docs/design/dependencies.md](docs/design/dependencies.md). In
short: a package is discussed before it is added, not after; its compatibility with GPLv3 is
shown, not assumed; and the reason is written here, so the next reader does not have to find it
again.
